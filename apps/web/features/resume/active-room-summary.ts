import type { I18nKey, RoomDto } from '@playme/shared';
import { playerLabel } from '@/lib/player-label';
import type { ActiveRoom } from './active-room-storage';

type T = (key: I18nKey) => string;
type Tf = (key: I18nKey, params: Readonly<Record<string, string | number>>) => string;

/**
 * Opponent + phase for a confirmed live room, as the short " · "-joined
 * fragments the resume surfaces show after the game name. The opponent is
 * the seat opposite the stored role; "your turn" compares the match's side
 * to move with the player's own seat side (both assigned by the time the
 * room is `inProgress`).
 */
export function describeActiveRoom(room: RoomDto, stored: ActiveRoom, t: T, tf: Tf): string[] {
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
