'use client';

import Link from 'next/link';
import { findGame, localizedHref, type I18nKey } from '@playme/shared';
import { track } from '@/lib/analytics';
import { useTranslator } from '@/lib/use-locale';
import { describeActiveRoom } from './active-room-summary';
import { useActiveRoom } from './use-active-room';

interface ActiveRoomCardProps {
  /** Headline, e.g. "Your game is still on" / "You still have a game running". */
  titleKey: I18nKey;
  /** Optional sentence under the summary, e.g. "Starting a new game abandons it." */
  noteKey?: I18nKey;
}

/**
 * Banner for a room this browser is still in: game, opponent and phase
 * from `useActiveRoom`, plus a return link to `/r/<code>`. Renders nothing
 * without a remembered live room. The landing page (`ResumeCard`) and the
 * configure page (`ActiveRoomNotice`) differ only in copy.
 */
export function ActiveRoomCard({ titleKey, noteKey }: ActiveRoomCardProps) {
  const { t, tf, locale } = useTranslator();
  const active = useActiveRoom();
  if (!active) return null;

  const { stored, room } = active;
  const game = findGame(stored.gameId);
  const gameName = game ? t(game.nameKey) : stored.gameId;
  const details = room ? describeActiveRoom(room, stored, t, tf) : [];

  return (
    <section className="resume-card" aria-label={t(titleKey)}>
      <ResumeIcon />
      <div className="resume-card__body">
        <strong>{t(titleKey)}</strong>
        <span className="resume-card__meta">{[gameName, ...details].join(' · ')}</span>
        {noteKey ? <span className="resume-card__note">{t(noteKey)}</span> : null}
      </div>
      <Link
        href={localizedHref(`/r/${stored.code}`, locale)}
        className="button-primary resume-card__cta"
        onClick={() => track({ name: 'room_resumed', props: { gameId: stored.gameId } })}
      >
        {t('resume.cta')}
      </Link>
    </section>
  );
}

function ResumeIcon() {
  return (
    <svg
      width="22"
      height="22"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
      className="resume-card__icon"
    >
      <circle cx="12" cy="12" r="9" />
      <polygon points="10 8 16 12 10 16 10 8" fill="currentColor" stroke="none" />
    </svg>
  );
}
