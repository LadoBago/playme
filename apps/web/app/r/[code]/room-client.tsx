'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useRouter } from 'next/navigation';
import {
  findGame,
  PlaymeClient,
  RoomHubClient,
  type I18nKey,
  type RoomDto,
  type Role,
  t,
} from '@playme/shared';
import { browserApiBase, hubUrl } from '@/lib/api-base';
import { findGameView } from '@/features/games/registry';
import { track } from '@/lib/analytics';
import { ConfirmDialog } from '@/components/confirm-dialog';
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
  const [room, setRoom] = useState<RoomDto>(initialRoom);
  const [role, setRole] = useState<Role | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [authStatus, setAuthStatus] = useState<'pending' | 'authed' | 'needsJoin'>('pending');
  const [connectionStatus, setConnectionStatus] = useState<
    'live' | 'reconnecting' | 'lost'
  >('live');
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
      onMatchStarted: ({ room: r }) => setRoom(r),
      onMoveAccepted: ({ room: r }) => setRoom(r),
      onMatchEnded: ({ room: r }) => {
        setRoom(r);
        // Fires on both clients — sender and receiver. We don't dedupe
        // server-side because PostHog already collapses by distinct_id
        // in insight queries; doing it here would require a separate
        // coordination event for no analytical benefit.
        const outcome = r.currentMatch?.outcome;
        if (outcome) {
          track({
            name: 'match_ended',
            props: { gameId: r.gameId, reason: outcome.kind },
          });
        }
      },
      onOpponentDisconnected: () => setRoom((prev) => ({ ...prev })),
      onOpponentReconnected: ({ room: r }) => setRoom(r),
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

  if (!game) {
    return <p className="banner banner--error">{t('errors.config.invalidGameId')}</p>;
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
      onSubmitMove={handleSubmitMove}
      onResign={handleResign}
      error={error}
      connectionStatus={connectionStatus}
    />
  );
}

interface MatchViewProps {
  room: RoomDto;
  role: Role | null;
  onSubmitMove: (payload: unknown) => void;
  onResign: () => Promise<void>;
  error: string | null;
  connectionStatus: 'live' | 'reconnecting' | 'lost';
}

function MatchView({
  room,
  role,
  onSubmitMove,
  onResign,
  error,
  connectionStatus,
}: MatchViewProps) {
  const router = useRouter();
  const match = room.currentMatch;
  const myPlayer = role === 'host' ? room.host : role === 'challenger' ? room.challenger : null;
  const opponent = role === 'host' ? room.challenger : role === 'challenger' ? room.host : null;
  const mySide = myPlayer?.side ?? null;
  const isMyTurn = match != null && match.outcome == null && mySide != null && mySide === match.sideToMove;
  const matchInProgress = match != null && match.outcome == null;
  const matchEnded = match != null && match.outcome != null;

  const [confirmResignOpen, setConfirmResignOpen] = useState(false);
  const [resignPending, setResignPending] = useState(false);

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

  const shareUrl = typeof window !== 'undefined' ? window.location.href : '';

  const GameView = findGameView(room.gameId);

  if (room.status === 'waitingForOpponent') {
    // Challenger inside this branch means the server has registered them
    // but the room hasn't flipped to InProgress yet — typically because
    // the host's SignalR isn't currently connected (tab backgrounded,
    // transient blip). Showing them the invite link would read as
    // "share this with someone" even though they ARE the someone. Hide
    // the share affordance and surface a neutral "starting" message;
    // MatchStarted will swap this view out once the host reconnects.
    const isChallenger = role === 'challenger';
    return (
      <div className="stack">
        <MatchHeader room={room} role={role} />
        {isChallenger ? null : <ShareLink url={shareUrl} />}
        <p style={{ color: 'var(--fg-muted)' }}>
          {isChallenger ? t('join.waitingForStart') : t('join.waiting')}
        </p>
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

      {matchEnded ? (
        <div className="match-controls">
          <button
            type="button"
            className="button-ghost"
            onClick={() => router.push('/')}
          >
            {t('match.backToLobby')}
          </button>
        </div>
      ) : null}

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
    </div>
  );
}

function OutcomeBanner({
  outcome,
  mySide,
}: {
  outcome: NonNullable<RoomDto['currentMatch']>['outcome'];
  mySide: string | null;
}) {
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
  return null;
}

function ConnectionHint({ room, role }: { room: RoomDto; role: Role | null }) {
  if (!role) return null;
  const opponentConnected = role === 'host' ? room.challengerConnected : room.hostConnected;
  const opponentRegistered = role === 'host' ? room.challenger != null : true;
  if (!opponentRegistered || opponentConnected) return null;
  return <p style={{ color: 'var(--fg-muted)' }}>{t('match.opponentDisconnected')}</p>;
}

function ShareLink({ url }: { url: string }) {
  const [copied, setCopied] = useState(false);
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
      </div>
    </div>
  );
}
