import { z } from "zod";

/**
 * Zod schema for the Reversi board state blob produced by the API-side
 * `ReversiGameModule.Serialize`. Per CLAUDE.md §7, game vocabulary (board
 * size, cells, lastPlacement, mustPassSide, …) stays inside the module;
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
  /** Last placement coord; absent after a pass or before any move. */
  lastPlacement: CoordSchema.optional(),
  lastWasPass: z.boolean(),
  /** Coords flipped by the last placement; empty after a pass / opening. */
  flippedLastTurn: z.array(CoordSchema).readonly().optional(),
  consecutivePasses: z.number().int().nonnegative(),
  /** Server-published flag — when set, the named side must auto-pass. */
  mustPassSide: z.string().optional(),
  /** Side that just passed (only meaningful with `lastWasPass: true`). The
   *  renderer keys per-side toast copy off this. */
  lastPassSide: z.string().optional(),
  darkCount: z.number().int().nonnegative(),
  lightCount: z.number().int().nonnegative(),
});

export type ReversiBoardState = z.infer<typeof ReversiBoardStateSchema>;
