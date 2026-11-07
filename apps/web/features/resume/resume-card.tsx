'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { PlaymeClient, findGame, localizedHref, type RoomDto } from '@playme/shared';
import { browserApiBase } from '@/lib/api-base';
import { track } from '@/lib/analytics';
import { playerLabel } from '@/lib/player-label';
import { useTranslator } from '@/lib/use-locale';
import {
  type ActiveRoom,
  clearActiveRoom,
  isResumableStatus,
  readActiveRoom,
} from './active-room-storage';

/**
 * "Your game is still on" card for the landing page. Renders nothing
 * server-side and nothing for a visitor without a remembered room, so the
 * SSR markup and the Lighthouse lab run are unaffected. For a returning
 * player it mounts immediately from the stored code + game (the return
 * link is the point, and in `inProgress` the reconnect grace is only
 * 60–90 s), then confirms with `GET /api/rooms/{code}` and fills in the
 * opponent and whose turn it is. A room that is gone or past the point of
 * return unmounts the card and forgets the key; a transport failure keeps
 * the card, since the room page is the authority on what's left.
 */
export function ResumeCard() {
  const { t, tf, locale } = useTranslator();
  const [stored, setStored] = useState<ActiveRoom | null>(null);
  const [room, setRoom] = useState<RoomDto | null>(null);

  useEffect(() => {
    const active = readActiveRoom();
    if (!active) return undefined;
    setStored(active);
    let cancelled = false;
    void (async () => {
      const client = new PlaymeClient({ baseUrl: browserApiBase });
      const result = await client.getRoom(active.code, { cache: 'no-store' });
      if (cancelled) return;
      if (result.ok) {
        if (isResumableStatus(result.value.status)) {
          setRoom(result.value);
          return;
        }
        // Post-match or terminal — nothing to come back to.
        clearActiveRoom(active.code);
        setStored(null);
        return;
      }
      if (result.status === 404) {
        // Reaped by the sweeper; the key outlived the room.
        clearActiveRoom(active.code);
        setStored(null);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  if (!stored) return null;

  const game = findGame(stored.gameId);
  const gameName = game ? t(game.nameKey) : stored.gameId;
  const details = room ? describe(room, stored, t, tf) : [];

  return (
    <section className="resume-card" aria-label={t('resume.title')}>
      <ResumeIcon />
      <div className="resume-card__body">
        <strong>{t('resume.title')}</strong>
        <span className="resume-card__meta">{[gameName, ...details].join(' · ')}</span>
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

type Translator = ReturnType<typeof useTranslator>;

/**
 * Opponent + phase for the confirmed room. The opponent is taken from the
 * seat opposite the stored role; "your turn" compares the match's side to
 * move with that seat's side (the server assigns sides at match start, so
 * both are present whenever the room is `inProgress`).
 */
function describe(
  room: RoomDto,
  stored: ActiveRoom,
  t: Translator['t'],
  tf: Translator['tf'],
): string[] {
  const me = stored.role === 'host' ? room.host : room.challenger;
  const opponent = stored.role === 'host' ? room.challenger : room.host;
  const parts: string[] = [];
  const opponentName = playerLabel(opponent, room.botLevel, t);
  if (opponentName) parts.push(tf('resume.opponent', { opponent: opponentName }));
  switch (room.status) {
    case 'waitingForOpponent':
      // A bot room only waits for the host's own socket to register
      // (docs/state.md §2.1), so "waiting for your friend" would mislead.
      if (opponent?.kind !== 'bot') parts.push(t('resume.status.waiting'));
      break;
    case 'settingUp':
      parts.push(t('resume.status.settingUp'));
      break;
    case 'inProgress': {
      const sideToMove = room.currentMatch?.sideToMove;
      if (sideToMove !== undefined && me?.side !== undefined) {
        parts.push(t(sideToMove === me.side ? 'match.yourTurn' : 'match.opponentTurn'));
      }
      break;
    }
    default:
      break;
  }
  return parts;
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
