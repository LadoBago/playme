// The room this browser last created or joined, so the landing page can
// offer a way back after an accidental tab close. The server already knows
// the room through the single-slot session cookie (docs/security.md §4),
// but that cookie is HttpOnly and host-only on the API origin, so the web
// app can't read it — this key is the client-side mirror, written at the
// same two moments the cookie is minted (create / join). Pure convenience:
// the server never sees it, and a stale value is harmless — the card
// confirms liveness with `GET /api/rooms/{code}` before offering a link,
// and the room page itself falls back to the join form on a cookie
// mismatch. Same failure handling as features/preferences: reads and
// writes swallow the SecurityError / QuotaExceededError that Safari
// private mode and ITP partitioning throw.

import { z } from 'zod';
import { RoleSchema, type RoomStatus } from '@playme/shared';

export const ACTIVE_ROOM_KEY = 'playme:activeRoom';

const ActiveRoomSchema = z.object({
  code: z.string().min(1).max(64),
  gameId: z.string().min(1).max(32),
  role: RoleSchema,
  /** Unix ms when the room was created / joined from this browser. */
  savedAt: z.number().int().nonnegative(),
});

/** The room this browser last entered, as remembered on create / join. */
export type ActiveRoom = z.infer<typeof ActiveRoomSchema>;

/**
 * Statuses a player can still return to (docs/state.md §2.1). Post-match
 * states are excluded on purpose: leaving them counts as an exit after the
 * 10 s grace, so a card for an `ended` room would point at a room that's
 * about to close.
 */
const RESUMABLE: ReadonlySet<RoomStatus> = new Set<RoomStatus>([
  'waitingForOpponent',
  'settingUp',
  'inProgress',
]);

export function isResumableStatus(status: RoomStatus): boolean {
  return RESUMABLE.has(status);
}

/** The remembered room, or `null` when none is stored / readable / valid. */
export function readActiveRoom(): ActiveRoom | null {
  if (typeof window === 'undefined') return null;
  try {
    const raw = window.localStorage.getItem(ACTIVE_ROOM_KEY);
    if (raw == null) return null;
    const parsed: unknown = JSON.parse(raw);
    const result = ActiveRoomSchema.safeParse(parsed);
    return result.success ? result.data : null;
  } catch {
    // Malformed JSON or storage access denied — behave as if nothing was saved.
    return null;
  }
}

export function writeActiveRoom(room: ActiveRoom): void {
  if (typeof window === 'undefined') return;
  try {
    window.localStorage.setItem(ACTIVE_ROOM_KEY, JSON.stringify(room));
  } catch {
    // Not persisted; the room still works — only the landing card is lost.
  }
}

/**
 * Forget the remembered room. With `code`, only clears when the stored
 * room is that one: a visitor who opens a friend's link for Room C while
 * still holding a live Room A must not lose Room A by declining to join C.
 */
export function clearActiveRoom(code?: string): void {
  if (typeof window === 'undefined') return;
  try {
    if (code !== undefined) {
      const stored = readActiveRoom();
      if (stored === null || stored.code !== code) return;
    }
    window.localStorage.removeItem(ACTIVE_ROOM_KEY);
  } catch {
    // Storage access denied — nothing was readable to begin with.
  }
}
