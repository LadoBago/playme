import { z } from "zod";

/**
 * Zod schema for the Reversi board state blob produced by the API-side
 * `ReversiGameModule.Serialize`. Per CLAUDE.md §7, game vocabulary (board
 * size, cells, lastPlacement, skippedSide, …) stays inside the module;
 * the platform handles only the opaque `MatchDto.state` string.
 */
const CoordSchema = z.object({
  row: z.number().int().nonnegative(),
  col: z.number().int().nonnegative(),
});

export const ReversiBoardStateSchema = z.object({
  size: z.number().int().positive(),
  moveCount: z.number().int().nonnegative(),
  cells: z.array(z.string().nullable()).readonly(),
  /** Last placement coord; absent before any move. */
  lastPlacement: CoordSchema.optional(),
  /** Coords flipped by the last placement; empty during the opening. */
  flippedLastTurn: z.array(CoordSchema).readonly().optional(),
  /** Side whose turn the server skipped because the last placement left
   *  them without a legal move (the mover kept the turn via seam B
   *  `MoveResult.KeepTurn`). The renderer keys per-side toast copy off
   *  this. */
  skippedSide: z.string().optional(),
  darkCount: z.number().int().nonnegative(),
  lightCount: z.number().int().nonnegative(),
});

export type ReversiBoardState = z.infer<typeof ReversiBoardStateSchema>;
