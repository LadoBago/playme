'use client';

import { type RoomDto, type Role } from '@playme/shared';
import { findGameModule } from '@/features/games/registry';
import { useTranslator } from '@/lib/use-locale';
import { SeriesScore } from './clock';

interface MatchHeaderProps {
  room: RoomDto;
  role: Role | null;
  /**
   * Show the series score in the header's middle column. Set during the
   * setup phase, when no clock row (which otherwise carries the score) is
   * rendered — so a rematch series keeps its running score visible.
   */
  showScore?: boolean;
}

export function MatchHeader({ room, role, showScore = false }: MatchHeaderProps) {
  const { t, locale } = useTranslator();
  // While role is null (initial hydrate — the SignalR JoinRoom round-trip
  // hasn't resolved the caller yet) fall back to host-on-left with the real
  // display names. The names come from the SSR snapshot, so the header — the
  // page's LCP element — paints on first render instead of waiting out the
  // handshake. The you/opponent caption is screen-reader-only now (the clock
  // strip below carries the visible label), so there's no layout shift to
  // reserve space for.
  const myPlayer = role === 'challenger' ? room.challenger : room.host;
  const opponentPlayer = role === 'challenger' ? room.host : room.challenger;

  // Resolve side identifiers through the per-game module so the platform
  // header never has to know "x"/"o" vs "red"/"yellow" (CLAUDE.md §7
  // "Platform thinness"). Unknown game → no side label, never the raw
  // identifier.
  const getSideLabel = findGameModule(room.gameId)?.getSideLabel;
  const mySideLabel = myPlayer?.side != null ? (getSideLabel?.(myPlayer.side, locale) ?? null) : null;
  const opponentSideLabel =
    opponentPlayer?.side != null ? (getSideLabel?.(opponentPlayer.side, locale) ?? null) : null;

  // Score from the viewer's perspective (host-on-left fallback while role is
  // null). Only worth showing during setup once a series is under way — the
  // first match's setup would just read 0 – 0.
  const myWins = role === 'challenger' ? room.score.challenger : room.score.host;
  const opponentWins = role === 'challenger' ? room.score.host : room.score.challenger;
  const seriesStarted = room.score.host + room.score.challenger + room.score.draws > 0;
  const showSetupScore = showScore && seriesStarted;

  return (
    <div className="match-meta">
      <PlayerCard
        roleLabel={t('match.you')}
        name={myPlayer?.displayName ?? '?'}
        sideLabel={mySideLabel}
      />
      {/* Middle cell: keeps the two names in the same grid columns as the two
          clock rectangles below. During setup (no clock row) it carries the
          running series score; otherwise it's an empty spacer. */}
      {showSetupScore ? (
        <SeriesScore myWins={myWins} opponentWins={opponentWins} draws={room.score.draws} />
      ) : (
        <span className="match-meta__gap" aria-hidden="true" />
      )}
      <PlayerCard
        roleLabel={t('match.opponent')}
        name={opponentPlayer?.displayName ?? '…'}
        sideLabel={opponentSideLabel}
      />
    </div>
  );
}

function PlayerCard({
  roleLabel,
  name,
  sideLabel,
}: {
  roleLabel: string;
  name: string;
  sideLabel: string | null;
}) {
  return (
    <div className="match-meta__player">
      {/* You/Opponent is shown on the clock strip below; keep it here for
          screen readers so the name still announces whose it is. */}
      <span className="visually-hidden">{roleLabel}</span>
      <span className="match-meta__name">
        {name}
        {sideLabel ? ` · ${sideLabel}` : ''}
      </span>
    </div>
  );
}
