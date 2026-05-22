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

  it('finds Connect 4 by its id', () => {
    const entry = findGame('connect4');
    expect(entry).toBeDefined();
    expect(entry?.rows).toBe(6);
    expect(entry?.cols).toBe(7);
    expect(entry?.sides.map((s) => s.id)).toEqual(['red', 'yellow']);
    expect(entry?.defaultHostSide).toBe('red');
  });

  it('finds Tic-Tac-Toe 6×6 by its id with X/O sides on a 6×6 board', () => {
    const entry = findGame('tictactoe-6x6');
    expect(entry).toBeDefined();
    expect(entry?.rows).toBe(6);
    expect(entry?.cols).toBe(6);
    expect(entry?.sides.map((s) => s.id)).toEqual(['x', 'o']);
    expect(entry?.defaultHostSide).toBe('x');
  });

  it('finds Tic-Tac-Toe 9×9 by its id with X/O sides on a 9×9 board', () => {
    const entry = findGame('tictactoe-9x9');
    expect(entry).toBeDefined();
    expect(entry?.rows).toBe(9);
    expect(entry?.cols).toBe(9);
    expect(entry?.sides.map((s) => s.id)).toEqual(['x', 'o']);
    expect(entry?.defaultHostSide).toBe('x');
  });

  it('finds Reversi by its id with dark/light sides on an 8×8 board', () => {
    const entry = findGame('reversi');
    expect(entry).toBeDefined();
    expect(entry?.rows).toBe(8);
    expect(entry?.cols).toBe(8);
    expect(entry?.sides.map((s) => s.id)).toEqual(['dark', 'light']);
    expect(entry?.defaultHostSide).toBe('dark');
  });

  it('returns undefined for an unknown slug', () => {
    expect(findGame('does-not-exist')).toBeUndefined();
  });

  it('every catalog entry has exactly two sides with a valid default', () => {
    for (const game of GAME_CATALOG) {
      expect(game.sides.length).toBe(2);
      expect(game.sides.some((s) => s.id === game.defaultHostSide)).toBe(true);
    }
  });
});
