import { describe, expect, it } from 'vitest';
import { findGame, GAME_CATALOG } from './games';

// Smoke tests for the shared package's catalog. Their job is mainly to
// prove the Vitest toolchain is wired (test runner + Node env + CI), but
// they double as a regression net for the lookup the configure page and
// room page both depend on.
describe('findGame', () => {
  it('finds the unified Tic-Tac-Toe entry by id', () => {
    const entry = findGame('tictactoe');
    expect(entry).toBeDefined();
    expect(entry?.id).toBe('tictactoe');
    expect(entry?.sides.map((s) => s.id)).toEqual(['x', 'o']);
    expect(entry?.defaultHostSide).toBe('x');
  });

  it('finds Connect 4 by its id', () => {
    const entry = findGame('connect4');
    expect(entry).toBeDefined();
    expect(entry?.rows).toBe(6);
    expect(entry?.cols).toBe(7);
    expect(entry?.sides.map((s) => s.id)).toEqual(['red', 'yellow']);
    expect(entry?.defaultHostSide).toBe('red');
  });

  it('finds Reversi by its id with dark/light sides on an 8×8 board', () => {
    const entry = findGame('reversi');
    expect(entry).toBeDefined();
    expect(entry?.rows).toBe(8);
    expect(entry?.cols).toBe(8);
    expect(entry?.sides.map((s) => s.id)).toEqual(['dark', 'light']);
    expect(entry?.defaultHostSide).toBe('dark');
  });

  it('finds Sea Battle by its id with first/second sides on a 10×10 board', () => {
    const entry = findGame('seabattle');
    expect(entry).toBeDefined();
    expect(entry?.rows).toBe(10);
    expect(entry?.cols).toBe(10);
    expect(entry?.preview).toHaveLength(100);
    expect(entry?.sides.map((s) => s.id)).toEqual(['first', 'second']);
    expect(entry?.defaultHostSide).toBe('first');
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
