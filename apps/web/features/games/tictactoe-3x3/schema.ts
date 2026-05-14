import { z } from 'zod';

/**
 * Zod schema for the TTT board state blob produced by the API-side
 * `TicTacToe3x3GameModule.Serialize`. Lives inside the game feature so
 * game-specific vocabulary stays in the module (CLAUDE.md §7 "Platform
 * thinness"). The platform never sees this — it only ships the opaque
 * `MatchDto.state` string.
 */
export const TttBoardStateSchema = z.object({
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

export type TttBoardState = z.infer<typeof TttBoardStateSchema>;
