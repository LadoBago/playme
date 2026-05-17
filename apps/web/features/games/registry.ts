import type { GameModule, GameView } from './types';
import { Connect4View, connect4SideLabel } from './connect4/view';
import { TicTacToe3x3View, tttSideLabel } from './tictactoe-3x3/view';

/**
 * Game-id → module. Adding a new game (Sprints 3–4) is one line here
 * plus a new `features/games/<game>/view.tsx` — zero edits in the platform
 * room shell (CLAUDE.md §7 "Platform thinness"). Per-game vocabulary
 * ("x"/"o", "red"/"yellow") stays inside the module: the platform only
 * ever sees the `GameModule` shape.
 */
const MODULES = new Map<string, GameModule>([
  ['tictactoe-3x3', { View: TicTacToe3x3View, getSideLabel: tttSideLabel }],
  ['connect4', { View: Connect4View, getSideLabel: connect4SideLabel }],
]);

export function findGameModule(gameId: string): GameModule | undefined {
  return MODULES.get(gameId);
}

export function findGameView(gameId: string): GameView | undefined {
  return MODULES.get(gameId)?.View;
}
