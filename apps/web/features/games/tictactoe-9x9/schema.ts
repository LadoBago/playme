import { z } from 'zod';

/**
 * Zod schema for the TTT 9×9 board state blob produced by the API-side
 * `TicTacToe9x9GameModule.Serialize`. Lives inside the game feature so
 * game-specific vocabulary stays in the module (CLAUDE.md §7 "Platform
 * thinness"). The platform never sees this — it only ships the opaque
 * `MatchDto.state` string. Independent of (and intentionally not shared
 * with) the analogous schema in the `tictactoe-3x3` module — per-module
 * duplication is acceptable.
 */
export const Ttt9x9BoardStateSchema = z.object({
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

export type Ttt9x9BoardState = z.infer<typeof Ttt9x9BoardStateSchema>;
