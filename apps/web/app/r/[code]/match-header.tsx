'use client';

import { type RoomDto, type Role, t, tf } from '@playme/shared';
import { findGameModule } from '@/features/games/registry';

interface MatchHeaderProps {
  room: RoomDto;
  role: Role | null;
}

export function MatchHeader({ room, role }: MatchHeaderProps) {
  const myPlayer = role === 'host' ? room.host : role === 'challenger' ? room.challenger : null;
  const opponentPlayer = role === 'host' ? room.challenger : role === 'challenger' ? room.host : null;

  // Resolve side identifiers through the per-game module so the platform
  // header never has to know "x"/"o" vs "red"/"yellow" (CLAUDE.md §7
  // "Platform thinness"). Unknown game → no side label, never the raw
  // identifier.
  const getSideLabel = findGameModule(room.gameId)?.getSideLabel;
  const mySideLabel = myPlayer?.side != null ? (getSideLabel?.(myPlayer.side) ?? null) : null;
  const opponentSideLabel =
    opponentPlayer?.side != null ? (getSideLabel?.(opponentPlayer.side) ?? null) : null;

  // Score is always rendered from the viewer's perspective so the left
  // number sits under the left card. When role is null (initial hydrate)
  // we fall back to host-on-left.
  const myWins = role === 'challenger' ? room.score.challenger : room.score.host;
  const opponentWins = role === 'challenger' ? room.score.host : room.score.challenger;

  return (
    <div className="match-meta">
      <PlayerCard
        label={t('match.you')}
        name={myPlayer?.displayName ?? '?'}
        sideLabel={mySideLabel}
      />
      <SeriesScore myWins={myWins} opponentWins={opponentWins} draws={room.score.draws} />
      <PlayerCard
        label={t('match.opponent')}
        name={opponentPlayer?.displayName ?? '…'}
        sideLabel={opponentSideLabel}
      />
    </div>
  );
}

function SeriesScore({
  myWins,
  opponentWins,
  draws,
}: {
  myWins: number;
  opponentWins: number;
  draws: number;
}) {
  return (
    <div className="match-score" aria-label={t('match.score.label')}>
      <span className="match-score__counts">
        {myWins}
        <span className="match-score__dash" aria-hidden="true">
          {' – '}
        </span>
        {opponentWins}
      </span>
      {draws > 0 ? (
        <span className="match-score__draws">
          {draws === 1 ? t('match.score.draws.one') : tf('match.score.draws.other', { count: draws })}
        </span>
      ) : null}
    </div>
  );
}

function PlayerCard({
  label,
  name,
  sideLabel,
}: {
  label: string;
  name: string;
  sideLabel: string | null;
}) {
  return (
    <div className="match-meta__player">
      <span className="match-meta__role">{label}</span>
      <span className="match-meta__name">
        {name}
        {sideLabel ? ` · ${sideLabel}` : ''}
      </span>
    </div>
  );
}
