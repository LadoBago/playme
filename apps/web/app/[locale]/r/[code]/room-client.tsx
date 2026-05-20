'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
  findGame,
  localizedHref,
  PlaymeClient,
  RoomHubClient,
  type I18nKey,
  type RoomDto,
  type Role,
} from '@playme/shared';
import { browserApiBase, hubUrl } from '@/lib/api-base';
import { findGameView } from '@/features/games/registry';
import { track } from '@/lib/analytics';
import { ConfirmDialog } from '@/components/confirm-dialog';
import { useTranslator } from '@/lib/use-locale';
import { JoinForm } from './join-form';
import { InviteSummary } from './invite-summary';
import { Clock } from './clock';
import { MatchHeader } from './match-header';

interface RoomClientProps {
  initialRoom: RoomDto;
}

/**
 * Fire-and-forget hub teardown for cleanup paths (StrictMode double-
 * mount, route change, retry after a failed probe). A stop() failure
 * during teardown means the transport was already gone — there's no
 * useful action and surfacing the error would spam the console on
 * every dev re-render. Centralised so each call site doesn't have to
 * carry its own explanatory comment.
 */
async function silentStop(hub: RoomHubClient | null | undefined): Promise<void> {
  if (!hub) return;
  try {
    await hub.stop();
  } catch {
    // intentional — see jsdoc
  }
}

/**
 * Top-level client component for the room page. Drives the SignalR
 * connection, decides whether to render the join form or the match UI,
 * and threads the room state from server events through to the board.
 *
 * Role detection: the session cookie is HttpOnly + encrypted, so the
 * client cannot decode it. We attempt SignalR.JoinRoom() on mount; the
 * server returns the caller's role alongside the room state. On failure
 * (no cookie / not authorized) we render the join form.
 */
export function RoomClient({ initialRoom }: RoomClientProps) {
  const { t, locale } = useTranslator();
  const [room, setRoom] = useState<RoomDto>(initialRoom);
  const [role, setRole] = useState<Role | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [authStatus, setAuthStatus] = useState<'pending' | 'authed' | 'needsJoin'>('pending');
  const [connectionStatus, setConnectionStatus] = useState<
    'live' | 'reconnecting' | 'lost'
  >('live');
  // True after the opponent declined a rematch — distinguishes the
  // "Opponent declined" notice from the generic "Opponent left" banner.
  // Both end the room in `closed`, so the room status alone can't say which.
  const [declined, setDeclined] = useState(false);
  // True after the server's RoomExpired SignalR event lands — the
  // WaitingForOpponent room reached its 30-min deadline without anyone
  // joining. Terminal UI; no recovery path other than "back to home".
  const [expired, setExpired] = useState(false);
  const hubRef = useRef<RoomHubClient | null>(null);

  // URL room code — passed to hub.joinRoom() so the server can reject a
  // stale cookie that belongs to a different room (see RoomHub.JoinRoom).
  // Stable for the component's lifetime; the page never swaps room codes.
  const expectedRoomCode = initialRoom.code;

  const game = useMemo(() => findGame(room.gameId), [room.gameId]);

  const connect = useCallback(async (signal: { cancelled: boolean }) => {
    setError(null);
    const hub = new RoomHubClient({ url: hubUrl() });

    hub.on({
      onOpponentJoined: ({ room: r }) => setRoom(r),
      onMatchStarted: ({ room: r }) => {
        setRoom(r);
        // Fires on both clients — sender and receiver. We don't dedupe
        // because PostHog collapses by distinct_id in insight queries;
        // coordinating dedup here would add a side-channel event for no
        // analytical benefit. Same precedent applies to move_made.
        // match_ended is authoritative — the API emits it server-side per
        // docs/observability-and-i18n.md §1.2, so this client does not.
        track({ name: 'match_started', props: { gameId: r.gameId } });
      },
      onMoveAccepted: ({ room: r }) => {
        setRoom(r);
        track({ name: 'move_made', props: { gameId: r.gameId } });
      },
      onMatchEnded: ({ room: r }) => setRoom(r),
      onOpponentDisconnected: () => setRoom((prev) => ({ ...prev })),
      onOpponentReconnected: ({ room: r }) => setRoom(r),
      onOpponentExited: ({ room: r }) => setRoom(r),
      onRematchOffered: ({ room: r }) => setRoom(r),
      onRematchDeclined: ({ room: r }) => {
        setRoom(r);
        setDeclined(true);
      },
      onRoomExpired: () => {
        // Server reaped the WaitingForOpponent room — its Redis state
        // is already gone. Flip to the terminal "expired" view and tear
        // down the hub; there's nothing to reconnect to. Auto-reconnect
        // would otherwise loop on join failures.
        setExpired(true);
        void silentStop(hubRef.current);
        hubRef.current = null;
      },
      onReconnecting: () => setConnectionStatus('reconnecting'),
      onReconnected: () => {
        // Transport is back — re-call JoinRoom so the server records the
        // presence (cancels its disconnect-grace entry) and we receive a
        // fresh room+clock snapshot.
        void (async () => {
          try {
            const session = await hub.joinRoom(expectedRoomCode);
            setRoom(session.room);
            setRole(session.role);
            setConnectionStatus('live');
          } catch {
            setConnectionStatus('lost');
          }
        })();
      },
      // onclose fires on any stop() — including the React StrictMode
      // dev double-mount cleanup and our own handleJoined teardown. Only
      // treat it as "connection lost" when we were already in the
      // reconnecting state (i.e. the auto-reconnect schedule exhausted);
      // an unsolicited onclose while live means we initiated it.
      onConnectionClosed: () =>
        setConnectionStatus((prev) => (prev === 'reconnecting' ? 'lost' : prev)),
    });

    try {
      await hub.start();
      // If the effect was cleaned up while negotiating (StrictMode dev
      // double-mount, fast route change), tear down here instead of
      // letting cleanup call stop() mid-negotiation — that surfaces as
      // "The connection was stopped during negotiation" in the console.
      if (signal.cancelled) {
        await silentStop(hub);
        return;
      }
      hubRef.current = hub;
      const session = await hub.joinRoom(expectedRoomCode);
      if (signal.cancelled) {
        await silentStop(hub);
        hubRef.current = null;
        return;
      }
      setRoom(session.room);
      setRole(session.role);
      setAuthStatus('authed');
    } catch (e) {
      if (signal.cancelled) return;
      // The probe failed — either the visitor has no session yet (the
      // common case: just opened the share link) or the JoinRoom call
      // surfaced an i18n error. Either way the join form is the right
      // next step; the form's own onSubmit shows any submit-time error.
      const message = e instanceof Error ? e.message : '';
      if (message && message !== 'errors.session.unauthorized') {
        // Surface only known i18n keys (HubException carries them as the
        // message); skip SignalR's own framework strings.
        if (message.startsWith('errors.')) {
          setError(t(message as I18nKey));
        }
      }
      setAuthStatus('needsJoin');
      await silentStop(hub);
      hubRef.current = null;
    }
  }, [expectedRoomCode]);

  useEffect(() => {
    const signal = { cancelled: false };
    void connect(signal);
    return () => {
      signal.cancelled = true;
      void silentStop(hubRef.current);
      hubRef.current = null;
    };
    // Intentionally bind only once on mount; reconnects flow through the
    // hub's automatic reconnect path. `connect` is stable for our purposes.
  }, []);

  const handleJoined = useCallback(async () => {
    setAuthStatus('pending');
    // Tear down any partial connection and try again with the fresh cookie.
    await silentStop(hubRef.current);
    hubRef.current = null;
    await connect({ cancelled: false });
  }, [connect]);

  const handleSubmitMove = useCallback((payload: unknown) => {
    void (async () => {
      setError(null);
      try {
        const updated = await hubRef.current?.submitMove({ payload });
        if (updated) setRoom(updated);
      } catch (e) {
        const message = e instanceof Error ? e.message : 'errors.unknown';
        setError(t(message as I18nKey));
      }
    })();
  }, []);

  // Resign throws if the hub call fails; MatchView shows the dialog
  // while it's in flight and surfaces the error in the same banner the
  // move pipeline uses. Promise-returning so the dialog can `await`
  // before flipping its own pending state back off.
  const handleResign = useCallback(async (): Promise<void> => {
    setError(null);
    try {
      const updated = await hubRef.current?.resign();
      if (updated) setRoom(updated);
    } catch (e) {
      const message = e instanceof Error ? e.message : 'errors.unknown';
      setError(t(message as I18nKey));
      throw e;
    }
  }, []);

  // ExitRoom is idempotent server-side (already-Closed returns success).
  // If the hub call fails for some other reason — bad state, rate limit —
  // we surface the error and stay; the user can retry. The Promise is
  // returned so MatchView can await before its router.push.
  const handleExit = useCallback(async (): Promise<void> => {
    setError(null);
    try {
      const updated = await hubRef.current?.exitRoom();
      if (updated) setRoom(updated);
    } catch (e) {
      const message = e instanceof Error ? e.message : 'errors.unknown';
      setError(t(message as I18nKey));
      throw e;
    }
  }, []);

  const handleOfferRematch = useCallback(async (): Promise<void> => {
    setError(null);
    try {
      const updated = await hubRef.current?.offerRematch();
      if (updated) {
        setRoom(updated);
        track({ name: 'rematch_offered', props: { gameId: updated.gameId } });
      }
    } catch (e) {
      const message = e instanceof Error ? e.message : 'errors.unknown';
      setError(t(message as I18nKey));
      throw e;
    }
  }, []);

  const handleAcceptRematch = useCallback(async (): Promise<void> => {
    setError(null);
    try {
      const updated = await hubRef.current?.acceptRematch();
      if (updated) {
        setRoom(updated);
        track({ name: 'rematch_accepted', props: { gameId: updated.gameId } });
      }
    } catch (e) {
      const message = e instanceof Error ? e.message : 'errors.unknown';
      setError(t(message as I18nKey));
      throw e;
    }
  }, []);

  const handleRejectRematch = useCallback(async (): Promise<void> => {
    setError(null);
    try {
      const updated = await hubRef.current?.rejectRematch();
      if (updated) {
        setRoom(updated);
        track({ name: 'rematch_rejected', props: { gameId: updated.gameId } });
      }
    } catch (e) {
      const message = e instanceof Error ? e.message : 'errors.unknown';
      setError(t(message as I18nKey));
      throw e;
    }
  }, []);

  if (!game) {
    return <p className="banner banner--error">{t('errors.config.invalidGameId')}</p>;
  }

  // Terminal: room expired (RoomExpired SignalR event). Wins over both
  // authStatus and connectionStatus because there's nothing left to
  // join or reconnect to.
  if (expired) {
    return (
      <main
        className="container stack"
        style={{ textAlign: 'center', gap: '1rem' }}
      >
        <h1 style={{ fontSize: '1.75rem' }}>{t('room.expired.title')}</h1>
        <p style={{ color: 'var(--fg-muted)' }}>{t('room.expired.body')}</p>
        <Link
          href={localizedHref('/', locale)}
          className="button-primary"
          style={{ alignSelf: 'center', textDecoration: 'none' }}
        >
          {t('room.expired.cta')}
        </Link>
      </main>
    );
  }

  if (authStatus === 'pending') {
    return <p style={{ color: 'var(--fg-muted)' }}>…</p>;
  }

  if (authStatus === 'needsJoin') {
    return (
      <div className="stack">
        <InviteSummary hostDisplayName={room.host.displayName} game={game} />
        <JoinForm
          room={room}
          sides={game.sides}
          onJoined={(updated) => {
            setRoom(updated);
            void handleJoined();
          }}
          client={new PlaymeClient({ baseUrl: browserApiBase })}
        />
      </div>
    );
  }

  return (
    <MatchView
      room={room}
      role={role}
      declined={declined}
      onSubmitMove={handleSubmitMove}
      onResign={handleResign}
      onExit={handleExit}
      onOfferRematch={handleOfferRematch}
      onAcceptRematch={handleAcceptRematch}
      onRejectRematch={handleRejectRematch}
      error={error}
      connectionStatus={connectionStatus}
    />
  );
}

interface MatchViewProps {
  room: RoomDto;
  role: Role | null;
  declined: boolean;
  onSubmitMove: (payload: unknown) => void;
  onResign: () => Promise<void>;
  onExit: () => Promise<void>;
  onOfferRematch: () => Promise<void>;
  onAcceptRematch: () => Promise<void>;
  onRejectRematch: () => Promise<void>;
  error: string | null;
  connectionStatus: 'live' | 'reconnecting' | 'lost';
}

function MatchView({
  room,
  role,
  declined,
  onSubmitMove,
  onResign,
  onExit,
  onOfferRematch,
  onAcceptRematch,
  onRejectRematch,
  error,
  connectionStatus,
}: MatchViewProps) {
  const router = useRouter();
  const { t, locale } = useTranslator();
  const match = room.currentMatch;
  const myPlayer = role === 'host' ? room.host : role === 'challenger' ? room.challenger : null;
  const opponent = role === 'host' ? room.challenger : role === 'challenger' ? room.host : null;
  const mySide = myPlayer?.side ?? null;
  const isMyTurn = match != null && match.outcome == null && mySide != null && mySide === match.sideToMove;
  const matchInProgress = match != null && match.outcome == null;

  const [confirmResignOpen, setConfirmResignOpen] = useState(false);
  const [resignPending, setResignPending] = useState(false);
  const [exitPending, setExitPending] = useState(false);
  const [offerPending, setOfferPending] = useState(false);
  const [acceptPending, setAcceptPending] = useState(false);
  const [confirmRejectOpen, setConfirmRejectOpen] = useState(false);

  const handleConfirmResign = useCallback(() => {
    setResignPending(true);
    void (async () => {
      try {
        await onResign();
      } catch {
        // Error already surfaced via the room-client error banner.
      } finally {
        setResignPending(false);
        setConfirmResignOpen(false);
      }
    })();
  }, [onResign]);

  // Back to lobby: tell the server we're leaving (so the other player
  // gets OpponentExited cleanly and the room moves to Closed) before
  // navigating away. If the hub call fails, the error banner surfaces;
  // we don't navigate in that case so the user can retry.
  const handleBackToLobby = useCallback(() => {
    setExitPending(true);
    void (async () => {
      try {
        await onExit();
        router.push(localizedHref('/', locale));
      } catch {
        setExitPending(false);
      }
    })();
  }, [onExit, router]);

  const handleOfferClick = useCallback(() => {
    setOfferPending(true);
    void (async () => {
      try {
        await onOfferRematch();
      } catch {
        // Error surfaced via the error banner.
      } finally {
        setOfferPending(false);
      }
    })();
  }, [onOfferRematch]);

  const handleAcceptClick = useCallback(() => {
    setAcceptPending(true);
    void (async () => {
      try {
        await onAcceptRematch();
      } catch {
        // Error surfaced via the error banner.
      } finally {
        setAcceptPending(false);
      }
    })();
  }, [onAcceptRematch]);

  // Reject auto-routes per §1 #10 asymmetric exit — the rejector goes
  // home, the offerer stays in the room with the decline notice.
  const handleConfirmReject = useCallback(() => {
    void (async () => {
      try {
        await onRejectRematch();
        router.push(localizedHref('/', locale));
      } catch {
        setConfirmRejectOpen(false);
      }
    })();
  }, [onRejectRematch, router]);

  const shareUrl = typeof window !== 'undefined' ? window.location.href : '';

  const GameView = findGameView(room.gameId);

  if (room.status === 'waitingForOpponent') {
    // Challenger inside this branch means the server has registered them
    // but TryStartMatch failed — almost always because the host's SignalR
    // dropped right before the challenger landed (mobile Safari/WebKit
    // throttles backgrounded WebSockets aggressively). The room self-heals
    // when the host's auto-reconnect re-runs RegisterPresenceHandler and
    // emits MatchStarted to the group; show a host-specific message in
    // the meantime so the wait isn't mysterious.
    const isChallenger = role === 'challenger';
    const waitingForHost = isChallenger && !room.hostConnected;
    const messageKey = isChallenger
      ? waitingForHost
        ? 'join.waitingForHost'
        : 'join.waitingForStart'
      : 'join.waiting';
    return (
      <div className="stack">
        <MatchHeader room={room} role={role} />
        {isChallenger ? null : <ShareLink url={shareUrl} />}
        <p style={{ color: 'var(--fg-muted)' }}>{t(messageKey)}</p>
      </div>
    );
  }

  if (!match) {
    return <p>…</p>;
  }

  if (!GameView) {
    return <p className="banner banner--error">{t('errors.config.invalidGameId')}</p>;
  }

  return (
    <div className="match-layout stack">
      <MatchHeader room={room} role={role} />

      <Clock snapshot={match.clock} callerRole={role} isFinal={match.outcome != null} />

      {connectionStatus !== 'live' ? (
        <div className="banner banner--error">
          {connectionStatus === 'reconnecting'
            ? t('match.reconnecting')
            : t('match.connectionLost')}
        </div>
      ) : null}

      {error ? <div className="banner banner--error">{error}</div> : null}

      {match.outcome ? (
        <OutcomeBanner outcome={match.outcome} mySide={mySide} />
      ) : (
        <span className={`match-turn`}>
          {isMyTurn ? t('match.yourTurn') : t('match.opponentTurn')}
        </span>
      )}

      <PostMatchStatus room={room} role={role} declined={declined} />

      <GameView
        matchState={match.state}
        callerSide={mySide}
        canPlay={isMyTurn}
        matchEnded={match.outcome != null}
        onSubmitMove={onSubmitMove}
      />

      {matchInProgress ? (
        <div className="match-controls">
          <button
            type="button"
            className="button-ghost"
            onClick={() => setConfirmResignOpen(true)}
            disabled={resignPending}
          >
            {t('match.resign.button')}
          </button>
        </div>
      ) : null}

      <PostMatchPanel
        room={room}
        role={role}
        offerPending={offerPending}
        acceptPending={acceptPending}
        exitPending={exitPending}
        onOffer={handleOfferClick}
        onAccept={handleAcceptClick}
        onRejectClick={() => setConfirmRejectOpen(true)}
        onBackToLobby={handleBackToLobby}
      />

      <ConnectionHint room={room} role={role} />

      {opponent ? null : <ShareLink url={shareUrl} />}

      <ConfirmDialog
        open={confirmResignOpen}
        title={t('match.resign.confirm.title')}
        body={t('match.resign.confirm.body')}
        confirmLabel={t('match.resign.confirm.yes')}
        cancelLabel={t('match.resign.confirm.cancel')}
        tone="danger"
        onConfirm={handleConfirmResign}
        onCancel={() => setConfirmResignOpen(false)}
      />

      <ConfirmDialog
        open={confirmRejectOpen}
        title={t('match.rematch.confirmReject.title')}
        body={t('match.rematch.confirmReject.body')}
        confirmLabel={t('match.rematch.confirmReject.yes')}
        cancelLabel={t('match.rematch.confirmReject.cancel')}
        tone="danger"
        onConfirm={handleConfirmReject}
        onCancel={() => setConfirmRejectOpen(false)}
      />
    </div>
  );
}

/**
 * Post-match panel — branches on room status + offerer role. Per
 * docs/platform-and-games.md §1 #10 the rematch handshake's asymmetric
 * exit lives entirely on the client side: the rejector auto-routes (via
 * the parent's handleConfirmReject) and the offerer stays here with the
 * decline notice + a manual "Back to lobby" button.
 */
function PostMatchPanel({
  room,
  role,
  offerPending,
  acceptPending,
  exitPending,
  onOffer,
  onAccept,
  onRejectClick,
  onBackToLobby,
}: {
  room: RoomDto;
  role: Role | null;
  offerPending: boolean;
  acceptPending: boolean;
  exitPending: boolean;
  onOffer: () => void;
  onAccept: () => void;
  onRejectClick: () => void;
  onBackToLobby: () => void;
}) {
  const { t } = useTranslator();
  const matchEnded = room.currentMatch != null && room.currentMatch.outcome != null;
  const isResponder =
    room.status === 'awaitingRematch' &&
    room.rematchOffererRole != null &&
    role != null &&
    room.rematchOffererRole !== role;
  const isOfferer =
    room.status === 'awaitingRematch' &&
    room.rematchOffererRole != null &&
    role != null &&
    room.rematchOffererRole === role;

  if (!matchEnded && room.status !== 'awaitingRematch' && room.status !== 'closed') {
    return null;
  }

  if (isResponder) {
    return (
      <div className="match-controls">
        <button
          type="button"
          className="button-ghost"
          onClick={onRejectClick}
        >
          {t('match.rematch.reject.button')}
        </button>
        <button
          type="button"
          className="button-primary"
          onClick={onAccept}
          disabled={acceptPending}
        >
          {t('match.rematch.accept.button')}
        </button>
      </div>
    );
  }

  if (isOfferer) {
    return (
      <div className="match-controls">
        <button
          type="button"
          className="button-ghost"
          onClick={onBackToLobby}
          disabled={exitPending}
        >
          {t('match.backToLobby')}
        </button>
      </div>
    );
  }

  // Ended (no offer yet) or Closed (after decline / opponent exit).
  const canOffer = room.status === 'ended';
  return (
    <div className="match-controls">
      <button
        type="button"
        className="button-ghost"
        onClick={onBackToLobby}
        disabled={exitPending}
      >
        {t('match.backToLobby')}
      </button>
      {canOffer ? (
        <button
          type="button"
          className="button-primary"
          onClick={onOffer}
          disabled={offerPending}
        >
          {t('match.rematch.offer.button')}
        </button>
      ) : null}
    </div>
  );
}

/**
 * Inline post-match status text — renders above the board so the player
 * sees every match-status update in the same visual band as the outcome
 * banner (rather than discovering some messages above the board and
 * others below).
 */
function PostMatchStatus({
  room,
  role,
  declined,
}: {
  room: RoomDto;
  role: Role | null;
  declined: boolean;
}) {
  const { t } = useTranslator();
  if (room.status === 'closed') {
    return (
      <div className="banner">
        {declined ? t('match.rematch.declined') : t('match.opponentLeft')}
      </div>
    );
  }
  if (room.status === 'awaitingRematch' && role != null && room.rematchOffererRole != null) {
    const isOfferer = room.rematchOffererRole === role;
    return (
      <div className="banner">
        {isOfferer ? t('match.rematch.waiting') : t('match.rematch.offered')}
      </div>
    );
  }
  return null;
}

function OutcomeBanner({
  outcome,
  mySide,
}: {
  outcome: NonNullable<RoomDto['currentMatch']>['outcome'];
  mySide: string | null;
}) {
  const { t } = useTranslator();
  if (!outcome) return null;
  if (outcome.kind === 'draw') return <div className="banner banner--win">{t('match.result.draw')}</div>;
  if (outcome.kind === 'win') {
    const youWon = mySide != null && outcome.winningSide === mySide;
    return (
      <div className={`banner ${youWon ? 'banner--win' : ''}`}>
        {youWon ? t('match.result.youWin') : t('match.result.youLose')}
      </div>
    );
  }
  if (outcome.kind === 'timeout') {
    const youTimedOut = mySide != null && outcome.timedOutSide === mySide;
    return (
      <div className={`banner ${youTimedOut ? '' : 'banner--win'}`}>
        {youTimedOut
          ? t('match.result.youTimedOut')
          : t('match.result.opponentTimedOut')}
      </div>
    );
  }
  if (outcome.kind === 'resign') {
    const youResigned = mySide != null && outcome.resigningSide === mySide;
    return (
      <div className={`banner ${youResigned ? '' : 'banner--win'}`}>
        {youResigned
          ? t('match.result.youResigned')
          : t('match.result.opponentResigned')}
      </div>
    );
  }
  if (outcome.kind === 'disconnect') {
    const youLost = mySide != null && outcome.losingSide === mySide;
    return (
      <div className={`banner ${youLost ? '' : 'banner--win'}`}>
        {youLost
          ? t('match.result.youDisconnected')
          : t('match.result.opponentDisconnected')}
      </div>
    );
  }
  return null;
}

function ConnectionHint({ room, role }: { room: RoomDto; role: Role | null }) {
  const { t } = useTranslator();
  if (!role) return null;
  const opponentConnected = role === 'host' ? room.challengerConnected : room.hostConnected;
  const opponentRegistered = role === 'host' ? room.challenger != null : true;
  if (!opponentRegistered || opponentConnected) return null;
  return <p style={{ color: 'var(--fg-muted)' }}>{t('match.opponentDisconnected')}</p>;
}

function ShareLink({ url }: { url: string }) {
  const { t } = useTranslator();
  const [copied, setCopied] = useState(false);

  // Web Share API is widely available on mobile (iOS Safari, Android
  // Chrome) and on Chromium-based desktop, but absent on Firefox /
  // desktop Safari. Feature-detect so the button doesn't appear on
  // browsers where the call would be a no-op. Memoising avoids a
  // re-render flash where the button mounts post-hydration; SSR
  // returns false consistently which is fine.
  const canShare = useMemo(
    () => typeof navigator !== 'undefined' && typeof navigator.share === 'function',
    [],
  );

  const handleShare = () => {
    void (async () => {
      // Recipient apps disagree about the Web Share API payload. Viber,
      // for example, reads only the `url` field and silently drops
      // `title` / `text`. Embedding the URL inside the `text` field and
      // omitting the standalone `url` keeps the invite message intact
      // across recipients (Viber, Mail, Telegram, WhatsApp, iOS Messages
      // all auto-link URLs they find inside text).
      const variants: I18nKey[] = [
        'join.shareLink.shareText.1',
        'join.shareLink.shareText.2',
        'join.shareLink.shareText.3',
        'join.shareLink.shareText.4',
      ];
      // Math.random is intentional — UI variety, not security.
      const key = variants[Math.floor(Math.random() * variants.length)] as I18nKey;
      try {
        await navigator.share({
          title: t('join.shareLink.shareTitle'),
          text: `${t(key)} ${url}`,
        });
      } catch (e) {
        // User dismissed the sheet — AbortError is expected; no other
        // error path is interesting enough to surface to the user.
        if (e instanceof Error && e.name !== 'AbortError') {
          // Other errors (e.g. NotAllowedError on insecure context)
          // are silent failures here — Copy link is always available
          // as the fallback path right next to this button.
          console.warn('[ShareLink] navigator.share failed', e);
        }
      }
    })();
  };

  return (
    <div className="stack" style={{ gap: '0.4rem' }}>
      <span className="label">{t('join.shareLink.label')}</span>
      <div className="share-link">
        <span className="share-link__url">{url}</span>
        <button
          type="button"
          className="button-ghost"
          onClick={() => {
            void (async () => {
              await navigator.clipboard.writeText(url);
              setCopied(true);
              setTimeout(() => setCopied(false), 1500);
            })();
          }}
        >
          {copied ? t('join.shareLink.copied') : t('join.shareLink.copy')}
        </button>
        {canShare ? (
          <button type="button" className="button-ghost" onClick={handleShare}>
            {t('join.shareLink.share')}
          </button>
        ) : null}
      </div>
    </div>
  );
}
