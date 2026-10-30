import { z } from 'zod';

/**
 * Zod schema for the Checkers state blob produced by the API-side
 * `CheckersGameModule.Serialize` (docs/games/checkers.md "State shape").
 * Per CLAUDE.md §7, game vocabulary (cell tokens, chain, legalMoves,
 * drawReason, …) stays inside the module; the platform handles only the
 * opaque `MatchDto.state` string.
 */
export const BOARD_SIZE = 8;

const CoordSchema = z.object({
  row: z.number().int().min(0).max(BOARD_SIZE - 1),
  col: z.number().int().min(0).max(BOARD_SIZE - 1),
});

export type CheckersCoord = z.infer<typeof CoordSchema>;

export const PIECE_TOKENS = ['white', 'black', 'white-king', 'black-king'] as const;

export type PieceToken = (typeof PIECE_TOKENS)[number];

const MoveInfoSchema = z.object({
  from: CoordSchema,
  to: CoordSchema,
  /** Present on a jump: the square of the piece being captured. */
  captured: CoordSchema.optional(),
});

export type CheckersMoveInfo = z.infer<typeof MoveInfoSchema>;

export const CheckersStateSchema = z.object({
  cells: z.array(z.enum(PIECE_TOKENS).nullable()).length(BOARD_SIZE * BOARD_SIZE).readonly(),
  sideToMove: z.enum(['white', 'black']),
  moveCount: z.number().int().nonnegative(),
  lastMove: MoveInfoSchema.optional(),
  /** Open multi-capture chain: the side to move must keep jumping with the
   *  piece at `at`; `captured` are the pending pieces still on the board. */
  chain: z
    .object({
      at: CoordSchema,
      captured: z.array(CoordSchema).readonly(),
    })
    .optional(),
  /** Server-computed legal moves for `sideToMove` — the renderer highlights
   *  from this list and never re-implements the rules. */
  legalMoves: z.array(MoveInfoSchema).readonly(),
  whiteCount: z.number().int().nonnegative(),
  blackCount: z.number().int().nonnegative(),
  kingMovePlies: z.number().int().nonnegative(),
  history: z.array(z.string()).readonly(),
  drawReason: z.enum(['repetition', 'kingMoves']).optional(),
});

export type CheckersState = z.infer<typeof CheckersStateSchema>;

export function indexOf(row: number, col: number): number {
  return row * BOARD_SIZE + col;
}

export function coordIndex(c: CheckersCoord): number {
  return indexOf(c.row, c.col);
}

/** Play happens on the dark squares only; a1 (row 7, col 0) is dark. */
export function isDarkSquare(row: number, col: number): boolean {
  return (row + col) % 2 === 1;
}

export function sideOf(piece: PieceToken | null): 'white' | 'black' | null {
  if (piece === null) return null;
  return piece.startsWith('white') ? 'white' : 'black';
}

export function isKing(piece: PieceToken | null): boolean {
  return piece === 'white-king' || piece === 'black-king';
}
