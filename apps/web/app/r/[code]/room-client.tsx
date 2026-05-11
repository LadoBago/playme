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
import { MatchHeader } from './match-header';

interface RoomClientProps {
  initialRoom: RoomDto;
}

/**
 * Top-level client component for the room page. Drives the SignalR
 * connection, decides whether to render the join form or the match UI,
 * and threads the room state from server events through to the board.
 *
 * Role detection: the page can't decode the (encrypted) session cookie
 * server-side, so we attempt SignalR.JoinRoom() on mount and use the
 * resolved role from the server's RegisterPresence response. On failure
 * we render the join form.
 */
export function RoomClient({ initialRoom }: RoomClientProps) {
  const [room, setRoom] = useState<RoomDto>(initialRoom);
  const [role, setRole] = useState<Role | null>(null);
  const [lastMove, setLastMove] = useState<{ cell: number; side: string } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [authStatus, setAuthStatus] = useState<'pending' | 'authed' | 'needsJoin'>('pending');
  const hubRef = useRef<RoomHubClient | null>(null);

  const game = useMemo(() => findGame(room.gameId), [room.gameId]);

  const inferRoleFrom = useCallback((freshRoom: RoomDto): Role | null => {
    // Without server help, we can't infer which seat is ours from the
    // RoomDto alone. The server-side equivalent of this lookup would
    // compare the cookie's playerId to room.host/challenger. Instead, we
    // exploit a property of the WaitingForOpponent flow: a user who
    // successfully calls JoinRoom on the hub is whichever seat sent the
    // request that minted their cookie. We resolve role by which seat
    // the freshly-bound presence flag flipped to true since the last tick.
    // For Sprint 1's bring-up, we treat the host as "first connection"
    // and the challenger as "second" via the auth-test sequence below.
    return room.hostConnected !== freshRoom.hostConnected
      ? 'host'
      : room.challengerConnected !== freshRoom.challengerConnected
        ? 'challenger'
        : null;
  }, [room.hostConnected, room.challengerConnected]);

  const connect = useCallback(async () => {
    setError(null);
    const hub = new RoomHubClient({ url: hubUrl() });
    hubRef.current = hub;

    hub.on({
      onOpponentJoined: ({ room: r }) => setRoom(r),
      onMatchStarted: ({ room: r }) => setRoom(r),
      onMoveAccepted: ({ room: r, cell, side }) => {
        setRoom(r);
        setLastMove({ cell, side });
      },
      onMatchEnded: ({ room: r }) => setRoom(r),
      onOpponentDisconnected: () => setRoom((prev) => ({ ...prev })),
    });

    try {
      await hub.start();
      const freshRoom = await hub.joinRoom();
      const inferredRole = inferRoleFrom(freshRoom);
      setRoom(freshRoom);
      setRole(inferredRole);
      setAuthStatus('authed');
    } catch (e) {
      // 401 / unauthorized → no session for this room. Surface the join
      // form. Any other failure is treated as transient; the SignalR
      // automatic reconnect handles the rest.
      const message = e instanceof Error ? e.message : 'errors.unknown';
      if (message === 'errors.session.unauthorized') {
        setAuthStatus('needsJoin');
      } else {
        setError(t(message as I18nKey));
        setAuthStatus('needsJoin');
      }
      await hub.stop().catch(() => {});
      hubRef.current = null;
    }
  }, [inferRoleFrom]);

  useEffect(() => {
    void connect();
    return () => {
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
    await connect();
  }, [connect]);

  const handleSubmitMove = useCallback((cell: number) => {
    void (async () => {
      setError(null);
      try {
        const updated = await hubRef.current?.submitMove({ cell });
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
    />
  );
}

interface MatchViewProps {
  room: RoomDto;
  role: Role | null;
  lastMove: { cell: number; side: string } | null;
  onSubmitMove: (cell: number) => void;
  error: string | null;
}

function MatchView({ room, role, lastMove, onSubmitMove, error }: MatchViewProps) {
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

  const winningCells = new Set<number>();
  if (match.outcome?.kind === 'win' && match.outcome.winningLine) {
    for (const c of match.outcome.winningLine) {
      winningCells.add(c.row * match.cols + c.col);
    }
  }

  return (
    <div className="match-layout stack">
      <MatchHeader room={room} role={role} />

      {error ? <div className="banner banner--error">{error}</div> : null}

      {match.outcome ? (
        <OutcomeBanner outcome={match.outcome} mySide={mySide} />
      ) : (
        <span className={`match-turn`}>
          {isMyTurn ? t('match.yourTurn') : t('match.opponentTurn')}
        </span>
      )}

      <Board
        rows={match.rows}
        cols={match.cols}
        cells={match.cells}
        lastMoveCell={lastMove?.cell ?? null}
        winningCells={winningCells}
        canPlay={isMyTurn}
        onCellClick={onSubmitMove}
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
