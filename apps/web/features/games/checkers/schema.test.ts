import { describe, expect, it } from 'vitest';
import { CheckersStateSchema, coordIndex, isDarkSquare, isKing, sideOf } from './schema';

// Pins the renderer's side of the module ↔ renderer wire agreement
// (docs/games/checkers.md "State shape"): a blob shaped like the server's
// `CheckersGameModule.Serialize` output parses, and the helpers the view
// keys its highlights off agree with the server's conventions.
describe('CheckersStateSchema', () => {
  const cells: (string | null)[] = Array.from({ length: 64 }, () => null);
  cells[coordIndex({ row: 4, col: 3 })] = 'white';
  cells[coordIndex({ row: 3, col: 2 })] = 'black';
  cells[coordIndex({ row: 0, col: 7 })] = 'black-king';

  const blob = {
    cells,
    sideToMove: 'white',
    moveCount: 12,
    lastMove: { from: { row: 2, col: 1 }, to: { row: 3, col: 2 } },
    chain: { at: { row: 4, col: 3 }, captured: [{ row: 5, col: 2 }] },
    legalMoves: [{ from: { row: 4, col: 3 }, to: { row: 2, col: 1 }, captured: { row: 3, col: 2 } }],
    whiteCount: 1,
    blackCount: 2,
    kingMovePlies: 0,
    history: ['....'],
  };

  it('parses a server-shaped state blob', () => {
    const parsed = CheckersStateSchema.parse(blob);
    expect(parsed.sideToMove).toBe('white');
    expect(parsed.chain?.at).toEqual({ row: 4, col: 3 });
    expect(parsed.legalMoves[0]?.captured).toEqual({ row: 3, col: 2 });
    expect(parsed.drawReason).toBeUndefined();
  });

  it('accepts the two draw reasons and nothing else', () => {
    expect(CheckersStateSchema.safeParse({ ...blob, drawReason: 'repetition' }).success).toBe(true);
    expect(CheckersStateSchema.safeParse({ ...blob, drawReason: 'kingMoves' }).success).toBe(true);
    expect(CheckersStateSchema.safeParse({ ...blob, drawReason: 'boredom' }).success).toBe(false);
  });

  it('rejects unknown piece tokens, wrong cell counts and unknown sides', () => {
    const badToken = [...cells];
    badToken[0] = 'queen';
    expect(CheckersStateSchema.safeParse({ ...blob, cells: badToken }).success).toBe(false);
    expect(CheckersStateSchema.safeParse({ ...blob, cells: cells.slice(1) }).success).toBe(false);
    expect(CheckersStateSchema.safeParse({ ...blob, sideToMove: 'red' }).success).toBe(false);
  });

  it('helpers follow the server conventions', () => {
    expect(isDarkSquare(7, 0)).toBe(true); // a1 is dark
    expect(isDarkSquare(0, 0)).toBe(false);
    expect(sideOf('white-king')).toBe('white');
    expect(sideOf('black')).toBe('black');
    expect(sideOf(null)).toBeNull();
    expect(isKing('black-king')).toBe(true);
    expect(isKing('white')).toBe(false);
    expect(coordIndex({ row: 1, col: 2 })).toBe(10);
  });
});
