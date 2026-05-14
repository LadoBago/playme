import { z } from 'zod';

/**
 * Zod schema for the Connect 4 board state blob produced by the API-side
 * `Connect4GameModule.Serialize`. Per CLAUDE.md §7, game vocabulary
 * (rows, cols, cell sides, lastMove) stays inside the module; the
 * platform handles only the opaque `MatchDto.state` string.
 */
const CoordSchema = z.object({
  row: z.number().int().nonnegative(),
  col: z.number().int().nonnegative(),
});

export const Connect4BoardStateSchema = z.object({
  rows: z.number().int().positive(),
  cols: z.number().int().positive(),
  cells: z.array(z.string().nullable()).readonly(),
  lastMove: CoordSchema.optional(),
  winningLine: z.array(CoordSchema).readonly().optional(),
});

export type Connect4BoardState = z.infer<typeof Connect4BoardStateSchema>;
