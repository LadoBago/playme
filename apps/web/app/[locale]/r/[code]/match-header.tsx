'use client';

import { type RoomDto, type Role } from '@playme/shared';
import { findGameModule } from '@/features/games/registry';
import { useTranslator } from '@/lib/use-locale';

interface MatchHeaderProps {
  room: RoomDto;
  role: Role | null;
}

export function MatchHeader({ room, role }: MatchHeaderProps) {
  const { t, locale } = useTranslator();
  // While role is null (initial hydrate — the SignalR JoinRoom round-trip
  // hasn't resolved the caller yet) fall back to host-on-left with the real
  // display names, mirroring the score fallback below. The names come from
  // the SSR snapshot, so the header — the page's LCP element — paints on
  // first render instead of waiting out the handshake. The "You"/"Opponent"
  // captions render a non-breaking space meanwhile so the line keeps its
  // height (no layout shift when they fill in).
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

  // Score is always rendered from the viewer's perspective so the left
  // number sits under the left card. When role is null (initial hydrate)
  // we fall back to host-on-left.
  const myWins = role === 'challenger' ? room.score.challenger : room.score.host;
  const opponentWins = role === 'challenger' ? room.score.host : room.score.challenger;

  return (
    <div className="match-meta">
      <PlayerCard
        label={role === null ? '\u00A0' : t('match.you')}
        name={myPlayer?.displayName ?? '?'}
        sideLabel={mySideLabel}
        wins={myWins}
      />
      <SeriesScore myWins={myWins} opponentWins={opponentWins} draws={room.score.draws} />
      <PlayerCard
        label={role === null ? '\u00A0' : t('match.opponent')}
        name={opponentPlayer?.displayName ?? '…'}
        sideLabel={opponentSideLabel}
        wins={opponentWins}
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
  const { t, tf } = useTranslator();
  // role="group" — aria-label is prohibited on a generic div (WCAG 4.1.2).
  return (
    <div className="match-score" role="group" aria-label={t('match.score.label')}>
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
  wins,
}: {
  label: string;
  name: string;
  sideLabel: string | null;
  wins: number;
}) {
  const { t, tf } = useTranslator();
  const winsLabel =
    wins === 1 ? t('match.score.wins.one') : tf('match.score.wins.other', { count: wins });
  return (
    <div className="match-meta__player">
      <span className="match-meta__role">{label}</span>
      <span className="match-meta__name">
        {name}
        {sideLabel ? ` · ${sideLabel}` : ''}
        <span className="match-meta__wins"> — {winsLabel}</span>
      </span>
    </div>
  );
}
