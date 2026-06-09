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
  type RoomExpiryReason,
  type Role,
} from '@playme/shared';
import { browserApiBase, hubUrl } from '@/lib/api-base';
import { findGameModule } from '@/features/games/registry';
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
 * Bound a promise so a hung network op (e.g. a `negotiate` fetch that
 * neither resolves nor rejects while offline) can't wedge a caller that
 * holds a lock across the await. Rejects with `label` on timeout; the
 * underlying op keeps running but the awaiter is freed.
 */
async function withTimeout<T>(promise: Promise<T>, ms: number, label: string): Promise<T> {
  let timer: ReturnType<typeof setTimeout> | undefined;
  const timeout = new Promise<never>((_resolve, reject) => {
    timer = setTimeout(() => reject(new Error(label)), ms);
  });
  try {
    return await Promise.race([promise, timeout]);
  } finally {
    if (timer) clearTimeout(timer);
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
  // Set when the server's RoomExpired SignalR event lands, to the reason
  // it carried: 'unjoined' (the WaitingForOpponent room reached its
  // 30-min deadline) or 'setupTimeout' (neither player committed setup
  // in time). Terminal UI; no recovery path other than "back to home".
  const [expired, setExpired] = useState<RoomExpiryReason | null>(null);
  const hubRef = useRef<RoomHubClient | null>(null);
  // Timestamp the tab became hidden; cleared on visible. Drives the
  // visibility-recovery effect — see the comment block on that effect
  // below for the timing rationale.
  const hiddenAtRef = useRef<number | null>(null);
  // Guards against overlapping recovery attempts — the stall watchdog,
  // the `online` event, and `visibilitychange` can all fire at once. A
  // ref (not state) so the second caller bails synchronously.
  const recoveringRef = useRef(false);
  // Drops rapid double-taps on the same cell while the previous SubmitMove
  // is still in flight. Without this gate, a lag-induced second click would
  // reach the server *after* the first move already flipped sideToMove, and
  // come back as `errors.move.notYourTurn` — the user did nothing wrong;
  // their first click was already accepted. A ref (not state) so the second
  // tap is rejected synchronously inside the same event loop turn as the
  // first.
  const movePendingRef = useRef(false);

  // URL room code — passed to hub.joinRoom() so the server can reject a
  // stale cookie that belongs to a different room (see RoomHub.JoinRoom).
  // Stable for the component's lifetime; the page never swaps room codes.
  const expectedRoomCode = initialRoom.code;

  const game = useMemo(() => findGame(room.gameId), [room.gameId]);

  const connect = useCallback(async (
    signal: { cancelled: boolean },
    opts: { recovery?: boolean } = {},
  ) => {
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
      // Setup games (Sprint 10 seam C): SetupStarted replaces MatchStarted
      // when the room enters settingUp; OpponentSetupCommitted is the
      // role-level readiness ping. Both just refresh the snapshot — the
      // game view renders the placement screen off room.status + state.
      onSetupStarted: ({ room: r }) => setRoom(r),
      onOpponentSetupCommitted: ({ room: r }) => setRoom(r),
      onMoveAccepted: ({ room: r }) => {
        setRoom(r);
        track({ name: 'move_made', props: { gameId: r.gameId } });
      },
      onMatchEnded: ({ room: r }) => setRoom(r),
      onOpponentDisconnected: ({ room: r }) => setRoom(r),
      onOpponentReconnected: ({ room: r }) => setRoom(r),
      onOpponentExited: ({ room: r }) => setRoom(r),
      onRematchOffered: ({ room: r }) => setRoom(r),
      onRematchDeclined: ({ room: r }) => {
        setRoom(r);
        setDeclined(true);
      },
      onRoomExpired: ({ reason }) => {
        // Server reaped the room — its Redis state is already gone.
        // Flip to the terminal "expired" view (copy picked by reason)
        // and tear down the hub; there's nothing to reconnect to.
        // Auto-reconnect would otherwise loop on join failures.
        setExpired(reason);
        void silentStop(hubRef.current);
        hubRef.current = null;
      },
      onReconnecting: () => setConnectionStatus('reconnecting'),
      onReconnected: () => {
        // Transport is back — re-call JoinRoom so the server records the
        // presence (cancels its disconnect-grace entry) and we receive a
        // fresh room+clock snapshot. Bound the join so a half-open
        // reconnected socket can't leave us stuck on 'reconnecting'.
        void (async () => {
          try {
            const session = await withTimeout(
              hub.joinRoom(expectedRoomCode),
              8_000,
              'join-timeout',
            );
            setRoom(session.room);
            setRole(session.role);
            setConnectionStatus('live');
          } catch {
            setConnectionStatus('reconnecting');
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
      // Bound start + join when this is a recovery rebuild: a `negotiate`
      // fetch can hang (rather than reject) while offline, and the caller
      // (recoverConnection) holds a guard across this await — an unbounded
      // hang would wedge that guard and kill every future recovery attempt.
      const OP_TIMEOUT_MS = 8_000;
      if (opts.recovery) {
        await withTimeout(hub.start(), OP_TIMEOUT_MS, 'start-timeout');
      } else {
        await hub.start();
      }
      // If the effect was cleaned up while negotiating (StrictMode dev
      // double-mount, fast route change), tear down here instead of
      // letting cleanup call stop() mid-negotiation — that surfaces as
      // "The connection was stopped during negotiation" in the console.
      if (signal.cancelled) {
        await silentStop(hub);
        return;
      }
      hubRef.current = hub;
      const session = opts.recovery
        ? await withTimeout(hub.joinRoom(expectedRoomCode), OP_TIMEOUT_MS, 'join-timeout')
        : await hub.joinRoom(expectedRoomCode);
      if (signal.cancelled) {
        await silentStop(hub);
        hubRef.current = null;
        return;
      }
      setRoom(session.room);
      setRole(session.role);
      setAuthStatus('authed');
      // A fresh connection is live by definition. Set it explicitly so a
      // rebuild via recoverConnection() (visibility / online / manual
      // retry) clears a stale 'reconnecting'/'lost' banner — a brand-new
      // start() never fires onReconnected, so nothing else would.
      setConnectionStatus('live');
    } catch (e) {
      if (signal.cancelled) return;
      await silentStop(hub);
      hubRef.current = null;
      if (opts.recovery) {
        // A recovery rebuild (stall watchdog / online / visibility) of an
        // already-authed session. A failure here is transient — almost
        // always still-offline — not "this visitor needs to join". Stay in
        // the match view and leave the status reconnecting so the watchdog
        // tries again; never fall back to the join form mid-match.
        setConnectionStatus('reconnecting');
        return;
      }
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
    }
  }, [expectedRoomCode]);

  useEffect(() => {
    // A room that loads already-expired (refresh / locale switch after
    // the RoomExpired event landed) is terminal — the expired screen
    // renders below without any live data, so don't open a socket to it.
    if (initialRoom.status === 'expired') return undefined;
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

  // Presence recovery: re-establish the room session as cheaply as
  // possible, falling back to a full rebuild. First try an idempotent
  // `hub.joinRoom` — on a healthy connection that just flips our
  // presence back to true (and lets `TryStartMatch` run if the opponent
  // joined while we were away) and returns the current snapshot. If the
  // connection is half-dead the call hangs until SignalR's own timeout,
  // so race against 4 s and, on timeout/failure, tear down and rebuild
  // from scratch: a fresh OnConnectedAsync re-adds us to the room group,
  // JoinRoom runs RegisterPresenceHandler, and TryStartMatch fires if
  // both sides are now connected. Shared by the visibility, `online`,
  // and manual-retry paths below.
  const recoverConnection = useCallback(async () => {
    if (recoveringRef.current) return;
    recoveringRef.current = true;
    try {
      const JOIN_TIMEOUT_MS = 4_000;
      const hub = hubRef.current;
      // No hub yet (initial mount still in flight) — nothing to recover.
      // A null hub during a stall means connect() will run on its own.
      let settled = false;
      const refresh: Promise<'ok' | 'failed'> = hub
        ? (async () => {
            try {
              const session = await hub.joinRoom(expectedRoomCode);
              if (settled) return 'ok';
              settled = true;
              setRoom(session.room);
              setRole(session.role);
              setConnectionStatus('live');
              return 'ok';
            } catch {
              if (settled) return 'failed';
              settled = true;
              return 'failed';
            }
          })()
        : Promise.resolve('failed');
      const timeout: Promise<'timeout'> = new Promise((resolve) =>
        setTimeout(() => {
          if (settled) return;
          settled = true;
          resolve('timeout');
        }, JOIN_TIMEOUT_MS),
      );
      const result = await Promise.race([refresh, timeout]);
      if (result === 'ok') return;
      // The cheap idempotent join didn't land (half-dead or wedged socket).
      // Build a brand-new connection — the programmatic equivalent of a
      // page refresh, which is what reliably recovers when SignalR's
      // in-place auto-reconnect can't re-establish through the proxy.
      await silentStop(hubRef.current);
      hubRef.current = null;
      await connect({ cancelled: false }, { recovery: true });
    } finally {
      recoveringRef.current = false;
    }
  }, [connect, expectedRoomCode]);

  // Backgrounded-tab presence recovery. When the host taps the share
  // sheet to send the invite, the browser backgrounds this tab and
  // throttles the SignalR keep-alive ping. Past ~30 s — the server's
  // default `ClientTimeoutInterval` — the server reaps the connection
  // and marks `HostConnected=false`. If the challenger joins during
  // that window, `RegisterPresenceHandler.TryStartMatch` returns false
  // (HostConnected is gone) and no `MatchStarted` event fires. The
  // host's own SignalR client still thinks the WebSocket is alive
  // until *its* `serverTimeoutInMilliseconds` (also 30 s) elapses, so
  // the room stays frozen for up to half a minute after the host
  // returns — long enough that users reach for refresh.
  //
  // The 15 s hidden-duration gate matches half the server's
  // `ClientTimeoutInterval` — under 30 s of background no reap can have
  // happened, so the refresh is wasted bandwidth on every alt-tab and
  // notification dropdown. If the server-side `ClientTimeoutInterval`
  // is ever lowered (apps/api/src/PlayMe.Api/DependencyInjection/
  // AddApi.cs `AddSignalR`), drop this constant in lockstep.
  useEffect(() => {
    const HIDDEN_REFRESH_THRESHOLD_MS = 15_000;
    const onVisibilityChange = () => {
      if (document.visibilityState === 'hidden') {
        hiddenAtRef.current = Date.now();
        return;
      }
      const since = hiddenAtRef.current;
      hiddenAtRef.current = null;
      if (since === null) return;
      if (Date.now() - since < HIDDEN_REFRESH_THRESHOLD_MS) return;
      void recoverConnection();
    };
    document.addEventListener('visibilitychange', onVisibilityChange);
    return () => document.removeEventListener('visibilitychange', onVisibilityChange);
  }, [recoverConnection]);

  // Network-restored recovery. The reconnect policy keeps retrying every
  // 4 s, but its timer is blind to the real connection state — if the
  // link returns mid-interval the client would idle out the rest of the
  // wait while the user stares at "Reconnecting…" with working internet.
  // The browser fires `online` the instant connectivity is back, so nudge
  // recovery immediately; recoverConnection() short-circuits to a cheap
  // idempotent join when the socket is in fact healthy.
  useEffect(() => {
    const onOnline = () => void recoverConnection();
    window.addEventListener('online', onOnline);
    return () => window.removeEventListener('online', onOnline);
  }, [recoverConnection]);

  // Stall watchdog — the backstop for drops that fire no event. A SignalR
  // server-timeout can elapse while `navigator.onLine` stays true (a
  // proxy / radio / server-pause hiccup, not the interface going down), so
  // no `online` event arrives; a foreground tab fires no `visibilitychange`
  // either; and SignalR's in-place auto-reconnect can wedge without ever
  // recovering (notably re-negotiating through the dev WebSocket proxy).
  // With nothing else firing, the room would spin on "Reconnecting…"
  // forever. So while we're not live, periodically force a full rebuild
  // (recoverConnection → fresh connection = what a manual refresh does,
  // which is known to recover) until the connection comes back.
  useEffect(() => {
    if (connectionStatus === 'live') return undefined;
    const STALL_REBUILD_MS = 10_000;
    const id = setInterval(() => void recoverConnection(), STALL_REBUILD_MS);
    return () => clearInterval(id);
  }, [connectionStatus, recoverConnection]);

  const handleManualReconnect = useCallback(() => {
    setConnectionStatus('reconnecting');
    void recoverConnection();
  }, [recoverConnection]);

  const handleJoined = useCallback(async () => {
    setAuthStatus('pending');
    // Tear down any partial connection and try again with the fresh cookie.
    await silentStop(hubRef.current);
    hubRef.current = null;
    await connect({ cancelled: false });
  }, [connect]);

  const handleSubmitMove = useCallback((payload: unknown) => {
    if (movePendingRef.current) return;
    movePendingRef.current = true;
    void (async () => {
      setError(null);
      try {
        const updated = await hubRef.current?.submitMove({ payload });
        if (updated) setRoom(updated);
      } catch (e) {
        const message = e instanceof Error ? e.message : 'errors.unknown';
        setError(t(message as I18nKey));
      } finally {
        movePendingRef.current = false;
      }
    })();
  }, []);

  // The one-and-final setup commit for setup games (Sprint 10 seam C).
  // Mirrors the move pipeline: opaque payload, error key in the banner.
  const handleSubmitSetup = useCallback((payload: unknown) => {
    if (movePendingRef.current) return;
    movePendingRef.current = true;
    void (async () => {
      setError(null);
      try {
        const updated = await hubRef.current?.submitSetup({ payload });
        if (updated) setRoom(updated);
      } catch (e) {
        const message = e instanceof Error ? e.message : 'errors.unknown';
        setError(t(message as I18nKey));
      } finally {
        movePendingRef.current = false;
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

  // Terminal: room expired. Either the RoomExpired SignalR event landed
  // live (reason carried on the payload), or the page loaded a room
  // that already expired (refresh / locale switch after the event) —
  // then the reason is derived from shape: an expired room that never
  // recruited a challenger is the unjoined 30-minute window; one with a
  // challenger can only be a setup timeout, because in-progress rooms
  // never transition to `expired`. Wins over both authStatus and
  // connectionStatus because there's nothing left to join or
  // reconnect to.
  const expiredReason =
    expired ??
    (room.status === 'expired'
      ? room.challenger == null
        ? 'unjoined'
        : 'setupTimeout'
      : null);
  if (expiredReason) {
    return (
      <main
        className="container stack"
        style={{ textAlign: 'center', gap: '1rem' }}
      >
        <h1 style={{ fontSize: '1.75rem' }}>{t('room.expired.title')}</h1>
        <p style={{ color: 'var(--fg-muted)' }}>{t(`room.expired.body.${expiredReason}`)}</p>
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
    // The SSR payload already carries the full room snapshot, so paint the
    // match header — the page's LCP element — immediately instead of holding
    // the whole page on an ellipsis until the SignalR handshake resolves the
    // caller's role (~1.5 s of pure render delay on mobile). MatchHeader's
    // null-role mode renders host-on-left from the snapshot.
    // waitingForOpponent stays on the bare ellipsis: the viewer is as likely
    // the invite recipient, and flashing match UI before the join form would
    // mislead.
    if (room.status === 'waitingForOpponent') {
      return <p style={{ color: 'var(--fg-muted)' }}>…</p>;
    }
    return (
      <div className="match-layout stack">
        <MatchHeader room={room} role={null} />
        <p style={{ color: 'var(--fg-muted)' }}>…</p>
      </div>
    );
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
      onSubmitSetup={handleSubmitSetup}
      onResign={handleResign}
      onExit={handleExit}
      onOfferRematch={handleOfferRematch}
      onAcceptRematch={handleAcceptRematch}
      onRejectRematch={handleRejectRematch}
      error={error}
      connectionStatus={connectionStatus}
      onReconnect={handleManualReconnect}
    />
  );
}

/** Back-arrow glyph for the circular `icon-link` affordance (same arrow
 *  as the configure page's back link). */
function BackArrowIcon() {
  return (
    <svg
      width="20"
      height="20"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      <path d="M19 12H5M12 19l-7-7 7-7" />
    </svg>
  );
}

interface MatchViewProps {
  room: RoomDto;
  role: Role | null;
  declined: boolean;
  onSubmitMove: (payload: unknown) => void;
  onSubmitSetup: (payload: unknown) => void;
  onResign: () => Promise<void>;
  onExit: () => Promise<void>;
  onOfferRematch: () => Promise<void>;
  onAcceptRematch: () => Promise<void>;
  onRejectRematch: () => Promise<void>;
  error: string | null;
  connectionStatus: 'live' | 'reconnecting' | 'lost';
  onReconnect: () => void;
}

function MatchView({
  room,
  role,
  declined,
  onSubmitMove,
  onSubmitSetup,
  onResign,
  onExit,
  onOfferRematch,
  onAcceptRematch,
  onRejectRematch,
  error,
  connectionStatus,
  onReconnect,
}: MatchViewProps) {
  const router = useRouter();
  const { t, locale } = useTranslator();
  const match = room.currentMatch;
  const myPlayer = role === 'host' ? room.host : role === 'challenger' ? room.challenger : null;
  const opponent = role === 'host' ? room.challenger : role === 'challenger' ? room.host : null;
  const mySide = myPlayer?.side ?? null;
  // settingUp (Sprint 10 seam C): the match aggregate exists but the clock
  // isn't running and there are no turns — the game view owns the whole
  // placement screen, so the turn line, clock, and resign control all sit
  // this phase out.
  const inSetup = room.status === 'settingUp';
  const isMyTurn =
    !inSetup && match != null && match.outcome == null && mySide != null && mySide === match.sideToMove;
  const matchInProgress = match != null && match.outcome == null && !inSetup;

  const [confirmResignOpen, setConfirmResignOpen] = useState(false);
  const [resignPending, setResignPending] = useState(false);
  const [exitPending, setExitPending] = useState(false);
  const [offerPending, setOfferPending] = useState(false);
  const [acceptPending, setAcceptPending] = useState(false);
  const [confirmRejectOpen, setConfirmRejectOpen] = useState(false);
  const [confirmLeaveSetupOpen, setConfirmLeaveSetupOpen] = useState(false);

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

  const gameModule = findGameModule(room.gameId);
  const GameView = gameModule?.View;
  const TurnStatusExtra = gameModule?.TurnStatusExtra;

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
    // No server "cancel" exists for WaitingForOpponent (docs/state.md §2.4:
    // disconnects in this state are transparent; the room expires by TTL).
    // So the back-to-lobby affordance here is a plain navigation link —
    // anyone reopening the invite re-enters the same seat.
    return (
      <div className="stack">
        <Link
          href={localizedHref('/', locale)}
          className="icon-link"
          aria-label={t('match.backToLobby')}
          style={{ alignSelf: 'flex-start' }}
        >
          <BackArrowIcon />
        </Link>
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
      {/* Setup-only back affordance (same circular arrow as the configure
          page), behind a confirmation so a stray tap doesn't yank the
          player out mid-placement. Leaving is client-only by design: no
          exit path exists in SettingUp (docs/state.md §2.4 — the setup
          deadline is the only adjudicating authority), so the player can
          change their mind and rejoin via the invite link while the
          setup timer runs. */}
      {inSetup ? (
        <button
          type="button"
          className="icon-link"
          aria-label={t('match.backToLobby')}
          style={{ alignSelf: 'flex-start' }}
          onClick={() => setConfirmLeaveSetupOpen(true)}
        >
          <BackArrowIcon />
        </button>
      ) : null}

      <MatchHeader room={room} role={role} />

      {inSetup ? null : (
        <Clock snapshot={match.clock} callerRole={role} isFinal={match.outcome != null} />
      )}

      {connectionStatus !== 'live' ? (
        <span className="match-status match-status--error">
          {connectionStatus === 'reconnecting'
            ? t('match.reconnecting')
            : t('match.connectionLost')}
          <button type="button" className="match-status-retry" onClick={onReconnect}>
            {t('match.reconnect')}
          </button>
        </span>
      ) : null}

      {error ? <span className="match-status match-status--error">{error}</span> : null}

      {match.outcome ? (
        <OutcomeBanner outcome={match.outcome} mySide={mySide} />
      ) : inSetup ? null : (
        <div className="match-status-row">
          <span className="match-status">
            {isMyTurn ? t('match.yourTurn') : t('match.opponentTurn')}
          </span>
          {/* Module-provided inline annotation (e.g. Sea Battle's
              hit/miss feedback) — same row as the turn pill. Opaque to
              the platform; most games don't register one. */}
          {TurnStatusExtra ? (
            <TurnStatusExtra matchState={match.state} callerSide={mySide} canPlay={isMyTurn} />
          ) : null}
        </div>
      )}

      <PostMatchStatus room={room} role={role} declined={declined} />

      <ConnectionHint room={room} role={role} />

      <GameView
        matchState={match.state}
        callerSide={mySide}
        canPlay={isMyTurn}
        matchEnded={match.outcome != null}
        onSubmitMove={onSubmitMove}
        setup={
          match.setup
            ? {
                mineCommitted:
                  role === 'challenger'
                    ? match.setup.challengerCommitted
                    : match.setup.hostCommitted,
                opponentCommitted:
                  role === 'challenger'
                    ? match.setup.hostCommitted
                    : match.setup.challengerCommitted,
              }
            : null
        }
        onSubmitSetup={onSubmitSetup}
      />

      {matchInProgress ? (
        <div className="match-controls">
          <button
            type="button"
            className="button-danger-soft"
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
        open={confirmLeaveSetupOpen}
        title={t('match.leaveSetup.confirm.title')}
        body={t('match.leaveSetup.confirm.body')}
        confirmLabel={t('match.leaveSetup.confirm.yes')}
        cancelLabel={t('match.leaveSetup.confirm.cancel')}
        tone="danger"
        onConfirm={() => router.push(localizedHref('/', locale))}
        onCancel={() => setConfirmLeaveSetupOpen(false)}
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
 * docs/platform.md §1 #10 the rematch handshake's asymmetric
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
          className="button-danger-soft"
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
      <div className="match-controls match-controls--split">
        <button
          type="button"
          className="text-link"
          onClick={onBackToLobby}
          disabled={exitPending}
          aria-label={t('match.backToLobby')}
        >
          <span aria-hidden="true">← </span>
          <span className="label-full">{t('match.backToLobby')}</span>
          <span className="label-short">{t('match.backToLobby.short')}</span>
        </button>
      </div>
    );
  }

  // Ended (no offer yet) or Closed (after decline / opponent exit).
  // Only offer rematch when the opponent is actually present — otherwise
  // the button would park the offerer in awaitingRematch with no one to
  // accept (e.g. the opponent dropped mid-game and the match ended on
  // timeout / disconnect / win-without-them). If they reconnect inside
  // the post-match grace, opponentConnected flips back and the button
  // reappears on the next render.
  const opponentConnected =
    role === 'host'
      ? room.challengerConnected
      : role === 'challenger'
        ? room.hostConnected
        : false;
  const canOffer = room.status === 'ended' && opponentConnected;
  return (
    <div className="match-controls match-controls--split">
      <button
        type="button"
        className="text-link"
        onClick={onBackToLobby}
        disabled={exitPending}
        aria-label={t('match.backToLobby')}
      >
        <span aria-hidden="true">← </span>
        <span className="label-full">{t('match.backToLobby')}</span>
        <span className="label-short">{t('match.backToLobby.short')}</span>
      </button>
      {canOffer ? (
        <button
          type="button"
          className="button-primary"
          onClick={onOffer}
          disabled={offerPending}
          aria-label={t('match.rematch.offer.button')}
        >
          <span className="label-full">{t('match.rematch.offer.button')}</span>
          <span className="label-short">{t('match.rematch.offer.short')}</span>
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
      <span className="match-status">
        {declined ? t('match.rematch.declined') : t('match.opponentLeft')}
      </span>
    );
  }
  if (room.status === 'awaitingRematch' && role != null && room.rematchOffererRole != null) {
    const isOfferer = room.rematchOffererRole === role;
    return (
      <span className="match-status">
        {isOfferer ? t('match.rematch.waiting') : t('match.rematch.offered')}
      </span>
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
  if (outcome.kind === 'draw') return <span className="match-status match-status--win">{t('match.result.draw')}</span>;
  if (outcome.kind === 'win') {
    const youWon = mySide != null && outcome.winningSide === mySide;
    return (
      <span className={`match-status ${youWon ? 'match-status--win' : ''}`}>
        {youWon ? t('match.result.youWin') : t('match.result.youLose')}
      </span>
    );
  }
  if (outcome.kind === 'timeout') {
    const youTimedOut = mySide != null && outcome.timedOutSide === mySide;
    return (
      <span className={`match-status ${youTimedOut ? '' : 'match-status--win'}`}>
        {youTimedOut
          ? t('match.result.youTimedOut')
          : t('match.result.opponentTimedOut')}
      </span>
    );
  }
  if (outcome.kind === 'resign') {
    const youResigned = mySide != null && outcome.resigningSide === mySide;
    return (
      <span className={`match-status ${youResigned ? '' : 'match-status--win'}`}>
        {youResigned
          ? t('match.result.youResigned')
          : t('match.result.opponentResigned')}
      </span>
    );
  }
  if (outcome.kind === 'disconnect') {
    const youLost = mySide != null && outcome.losingSide === mySide;
    return (
      <span className={`match-status ${youLost ? '' : 'match-status--win'}`}>
        {youLost
          ? t('match.result.youDisconnected')
          : t('match.result.opponentDisconnected')}
      </span>
    );
  }
  return null;
}

function ConnectionHint({ room, role }: { room: RoomDto; role: Role | null }) {
  const { t } = useTranslator();
  if (!role) return null;
  // Only meaningful during active play. Post-match states render their own
  // status (PostMatchStatus banner above the board, OutcomeBanner) — a
  // second "opponent disconnected" line beneath the board duplicates that
  // (and contradicts it once the room transitions to closed).
  if (room.status !== 'inProgress') return null;
  const opponentConnected = role === 'host' ? room.challengerConnected : room.hostConnected;
  const opponentRegistered = role === 'host' ? room.challenger != null : true;
  if (!opponentRegistered || opponentConnected) return null;
  return <span className="match-status">{t('match.opponentDisconnected')}</span>;
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
        // NotAllowedError-style failures can only occur on insecure
        // contexts, which production HTTPS rules out.
        if (e instanceof Error && e.name !== 'AbortError') {
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
        {canShare ? (
          <button type="button" className="button-primary" onClick={handleShare}>
            {t('join.shareLink.share')}
          </button>
        ) : (
          <button
            type="button"
            className="button-primary"
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
        )}
      </div>
    </div>
  );
}
