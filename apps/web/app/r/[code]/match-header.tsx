'use client';

import { type RoomDto, type Role, t } from '@playme/shared';
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

  return (
    <div className="match-meta">
      <PlayerCard
        label={t('match.you')}
        name={myPlayer?.displayName ?? '?'}
        sideLabel={mySideLabel}
      />
      <PlayerCard
        label={t('match.opponent')}
        name={opponentPlayer?.displayName ?? '…'}
        sideLabel={opponentSideLabel}
      />
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
