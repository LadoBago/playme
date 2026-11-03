// Remembered configure choices (display name, last opponent tab, bot level,
// side, board size) so the next visit opens pre-filled. Pure convenience:
// the server never sees these, nothing depends on them persisting, and
// every read is validated — localStorage is an external input like any
// other (CLAUDE.md §6). Same failure handling as features/theme: reads
// and writes swallow the SecurityError / QuotaExceededError that Safari
// private mode and ITP partitioning throw.

import { z } from 'zod';
import { BotLevelSchema, SideSelectionModeSchema } from '@playme/shared';

export const DISPLAY_NAME_KEY = 'playme:displayName';
export const GAME_PREFS_KEY_PREFIX = 'playme:configure:';

/** Mirrors the `maxLength` of the name inputs and the server's cap. */
export const MAX_DISPLAY_NAME_LENGTH = 24;

/**
 * Each field is parsed on its own and dropped when invalid, so a stale or
 * partially corrupt blob (e.g. after a future shape change) keeps whatever
 * still makes sense instead of resetting everything.
 */
const GamePrefsSchema = z.object({
  opponent: z.enum(['friend', 'bot']).optional().catch(undefined),
  botLevel: BotLevelSchema.optional().catch(undefined),
  sideMode: SideSelectionModeSchema.optional().catch(undefined),
  hostSide: z.string().min(1).max(32).optional().catch(undefined),
  boardSize: z.number().int().positive().optional().catch(undefined),
});

/** The last choices made on one game's configure form. All fields optional. */
export type GamePrefs = z.infer<typeof GamePrefsSchema>;

export function gamePrefsKey(gameId: string): string {
  return `${GAME_PREFS_KEY_PREFIX}${gameId}`;
}

/** Last submitted display name, or `''` when none is stored / readable. */
export function readStoredDisplayName(): string {
  if (typeof window === 'undefined') return '';
  try {
    const raw = window.localStorage.getItem(DISPLAY_NAME_KEY);
    if (typeof raw !== 'string') return '';
    return raw.trim().slice(0, MAX_DISPLAY_NAME_LENGTH);
  } catch {
    return '';
  }
}

export function writeStoredDisplayName(name: string): void {
  if (typeof window === 'undefined') return;
  const trimmed = name.trim().slice(0, MAX_DISPLAY_NAME_LENGTH);
  if (trimmed.length === 0) return;
  try {
    window.localStorage.setItem(DISPLAY_NAME_KEY, trimmed);
  } catch {
    // Not persisted; the form still submits.
  }
}

/** Last configure choices for `gameId`, or `{}` when none / unreadable. */
export function readStoredGamePrefs(gameId: string): GamePrefs {
  if (typeof window === 'undefined') return {};
  try {
    const raw = window.localStorage.getItem(gamePrefsKey(gameId));
    if (raw == null) return {};
    const parsed: unknown = JSON.parse(raw);
    const result = GamePrefsSchema.safeParse(parsed);
    return result.success ? result.data : {};
  } catch {
    // Malformed JSON or storage access denied — behave as if nothing was saved.
    return {};
  }
}

export function writeStoredGamePrefs(gameId: string, prefs: GamePrefs): void {
  if (typeof window === 'undefined') return;
  try {
    window.localStorage.setItem(gamePrefsKey(gameId), JSON.stringify(prefs));
  } catch {
    // Not persisted; the room is still created.
  }
}
