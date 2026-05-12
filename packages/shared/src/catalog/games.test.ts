import { describe, expect, it } from 'vitest';
import { findGame, GAME_CATALOG } from './games';

// Smoke tests for the shared package's catalog. Their job is mainly to
// prove the Vitest toolchain is wired (test runner + Node env + CI), but
// they double as a regression net for the lookup the configure page and
// room page both depend on.
describe('findGame', () => {
  it('finds an entry by its id', () => {
    const entry = findGame('tictactoe-3x3');
    expect(entry).toBeDefined();
    expect(entry?.id).toBe('tictactoe-3x3');
  });

  it('returns undefined for an unknown slug', () => {
    expect(findGame('does-not-exist')).toBeUndefined();
  });

  it('every catalog entry has both x/o-style sides registered', () => {
    for (const game of GAME_CATALOG) {
      expect(game.sides.length).toBe(2);
      expect(game.sides.some((s) => s.id === game.defaultHostSide)).toBe(true);
    }
  });
});
