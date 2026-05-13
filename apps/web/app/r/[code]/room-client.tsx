'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
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
import { JoinForm } from './join-form';
import { Board } from './board';
import { Clock } from './clock';
import { MatchHeader } from './match-header';

/**
 * PR 3 TODO: extract the per-game state parse and cell renderer into a
 * per-game web module registered via the catalog. For PR 2 we keep them
 * inline because TTT 3×3 is still the only game in the catalog; the
 * platform shell (this file) is still TTT-aware. The platform `Board`
 * itself (see ./board.tsx) is already game-agnostic.
 */
interface TttBoardState {
  rows: number;
  cols: number;
  cells: readonly (string | null)[];
}

function parseTttState(state: string): TttBoardState {
  return JSON.parse(state) as TttBoardState;
}

function renderTttCell(side: string | null): string {
  if (side === null) return '';
  if (side === 'x') return '✕';
  if (side === 'o') return '◯';
  return side.toUpperCase();
}

interface RoomClientProps {
  initialRoom: RoomDto;
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
  const [lastMove, setLastMove] = useState<{ cell: number; side: string } | null>(null);
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
      onMoveAccepted: ({ room: r, cell, side }) => {
        setRoom(r);
        setLastMove({ cell, side });
      },
      onMatchEnded: ({ room: r }) => setRoom(r),
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
        await hub.stop().catch(() => {});
        return;
      }
      hubRef.current = hub;
      const session = await hub.joinRoom(expectedRoomCode);
      if (signal.cancelled) {
        await hub.stop().catch(() => {});
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
      await hub.stop().catch(() => {});
      hubRef.current = null;
    }
  }, [expectedRoomCode]);

  useEffect(() => {
    const signal = { cancelled: false };
    void connect(signal);
    return () => {
      signal.cancelled = true;
      hubRef.current?.stop().catch(() => {});
      hubRef.current = null;
    };
    // Intentionally bind only once on mount; reconnects flow through the
    // hub's automatic reconnect path. `connect` is stable for our purposes.
  }, []);

  const handleJoined = useCallback(async () => {
    setAuthStatus('pending');
    // Tear down any partial connection and try again with the fresh cookie.
    await hubRef.current?.stop().catch(() => {});
    hubRef.current = null;
    await connect({ cancelled: false });
  }, [connect]);

  const handleSubmitMove = useCallback((cell: number) => {
    void (async () => {
      setError(null);
      try {
        const updated = await hubRef.current?.submitMove({ payload: { cell } });
        if (updated) setRoom(updated);
      } catch (e) {
        const message = e instanceof Error ? e.message : 'errors.unknown';
        setError(t(message as I18nKey));
      }
    })();
  }, []);

  if (!game) {
    return <p className="banner banner--error">{t('errors.config.invalidGameId')}</p>;
  }

  if (authStatus === 'pending') {
    return <p style={{ color: 'var(--fg-muted)' }}>…</p>;
  }

  if (authStatus === 'needsJoin') {
    return (
      <JoinForm
        room={room}
        sides={game.sides}
        onJoined={(updated) => {
          setRoom(updated);
          void handleJoined();
        }}
        client={new PlaymeClient({ baseUrl: browserApiBase })}
      />
    );
  }

  return (
    <MatchView
      room={room}
      role={role}
      lastMove={lastMove}
      onSubmitMove={handleSubmitMove}
      error={error}
      connectionStatus={connectionStatus}
    />
  );
}

interface MatchViewProps {
  room: RoomDto;
  role: Role | null;
  lastMove: { cell: number; side: string } | null;
  onSubmitMove: (cell: number) => void;
  error: string | null;
  connectionStatus: 'live' | 'reconnecting' | 'lost';
}

function MatchView({
  room,
  role,
  lastMove,
  onSubmitMove,
  error,
  connectionStatus,
}: MatchViewProps) {
  const match = room.currentMatch;
  const myPlayer = role === 'host' ? room.host : role === 'challenger' ? room.challenger : null;
  const opponent = role === 'host' ? room.challenger : role === 'challenger' ? room.host : null;
  const mySide = myPlayer?.side ?? null;
  const isMyTurn = match != null && match.outcome == null && mySide != null && mySide === match.sideToMove;

  const shareUrl = typeof window !== 'undefined' ? window.location.href : '';

  if (room.status === 'waitingForOpponent') {
    return (
      <div className="stack">
        <MatchHeader room={room} role={role} />
        <ShareLink url={shareUrl} />
        <p style={{ color: 'var(--fg-muted)' }}>{t('join.waiting')}</p>
      </div>
    );
  }

  if (!match) {
    return <p>…</p>;
  }

  const boardState = parseTttState(match.state);

  const winningCells = new Set<number>();
  if (match.outcome?.kind === 'win' && match.outcome.winningLine) {
    for (const c of match.outcome.winningLine) {
      winningCells.add(c.row * boardState.cols + c.col);
    }
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

      <Board
        rows={boardState.rows}
        cols={boardState.cols}
        cells={boardState.cells}
        lastMoveCell={lastMove?.cell ?? null}
        winningCells={winningCells}
        canPlay={isMyTurn}
        onCellClick={onSubmitMove}
        renderCell={renderTttCell}
      />

      <ConnectionHint room={room} role={role} />

      {opponent ? null : <ShareLink url={shareUrl} />}
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
