import { z } from 'zod';

/**
 * Zod schema for the TTT-6×6 board state blob produced by the API-side
 * `TicTacToe6x6GameModule.Serialize`. Lives inside the game feature so
 * game-specific vocabulary stays in the module (CLAUDE.md §7 "Platform
 * thinness"). The platform never sees this — it only ships the opaque
 * `MatchDto.state` string.
 *
 * Intentionally **not** sharing the schema with the 3×3 (or future 9×9)
 * module even though the wire shape is structurally similar — per-module
 * duplication is the contract; see CLAUDE.md §7.
 */
export const Ttt6x6BoardStateSchema = z.object({
  rows: z.number().int().positive(),
  cols: z.number().int().positive(),
  cells: z.array(z.string().nullable()).readonly(),
  lastMove: z.number().int().nonnegative().optional(),
  winningLine: z
    .array(
      z.object({
        row: z.number().int().nonnegative(),
        col: z.number().int().nonnegative(),
      }),
    )
    .readonly()
    .optional(),
});

export type Ttt6x6BoardState = z.infer<typeof Ttt6x6BoardStateSchema>;
