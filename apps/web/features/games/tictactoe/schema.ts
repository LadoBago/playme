import { z } from 'zod';

/**
 * Zod schema for the unified Tic-Tac-Toe board state blob produced by the
 * API-side `TicTacToeGameModule.Serialize` (Sprint 9 PR1b). The server
 * picks `rows`/`cols`/`winLength` from the host-chosen `boardSize` option;
 * the renderer only reads them to size the grid and identify the winning
 * run. Lives inside the game feature so per-game vocabulary stays in the
 * module (CLAUDE.md §7 "Platform thinness"). The platform never sees this
 * — it only ships the opaque `MatchDto.state` string.
 */
export const TicTacToeBoardStateSchema = z.object({
  rows: z.number().int().positive(),
  cols: z.number().int().positive(),
  // winLength is informational — the server derives it from boardSize and
  // ships it for self-describing rehydration. The renderer reads
  // `winningLine` directly rather than re-deriving from runs.
  winLength: z.number().int().positive(),
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

export type TicTacToeBoardState = z.infer<typeof TicTacToeBoardStateSchema>;
