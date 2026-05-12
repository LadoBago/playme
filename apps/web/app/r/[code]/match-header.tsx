'use client';

import { type RoomDto, type Role, t } from '@playme/shared';

interface MatchHeaderProps {
  room: RoomDto;
  role: Role | null;
}

export function MatchHeader({ room, role }: MatchHeaderProps) {
  const myPlayer = role === 'host' ? room.host : role === 'challenger' ? room.challenger : null;
  const opponentPlayer = role === 'host' ? room.challenger : role === 'challenger' ? room.host : null;

  return (
    <div className="match-meta">
      <PlayerCard label={t('match.you')} name={myPlayer?.displayName ?? '?'} side={myPlayer?.side ?? null} />
      <PlayerCard
        label={t('match.opponent')}
        name={opponentPlayer?.displayName ?? '…'}
        side={opponentPlayer?.side ?? null}
      />
    </div>
  );
}

function PlayerCard({
  label,
  name,
  side,
}: {
  label: string;
  name: string;
  side: string | null;
}) {
  return (
    <div className="match-meta__player">
      <span className="match-meta__role">{label}</span>
      <span className="match-meta__name">
        {name}
        {side ? ` · ${side.toUpperCase()}` : ''}
      </span>
    </div>
  );
}
