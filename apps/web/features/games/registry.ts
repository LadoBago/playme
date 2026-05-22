import type { GameModule, GameView } from './types';
import { Connect4View, connect4SideLabel } from './connect4/view';
import { ReversiView, reversiSideLabel } from './reversi/view';
import { TicTacToeView, tictactoeSideLabel } from './tictactoe/view';

/**
 * Game-id → module. Adding a new game is one line here plus a new
 * `features/games/<game>/view.tsx` — zero edits in the platform room
 * shell (CLAUDE.md §7 "Platform thinness"). Per-game vocabulary ("x"/"o",
 * "red"/"yellow", "dark"/"light") stays inside the module: the platform
 * only ever sees the `GameModule` shape.
 */
const MODULES = new Map<string, GameModule>([
  ['tictactoe', { View: TicTacToeView, getSideLabel: tictactoeSideLabel }],
  ['connect4', { View: Connect4View, getSideLabel: connect4SideLabel }],
  ['reversi', { View: ReversiView, getSideLabel: reversiSideLabel }],
]);

export function findGameModule(gameId: string): GameModule | undefined {
  return MODULES.get(gameId);
}

export function findGameView(gameId: string): GameView | undefined {
  return MODULES.get(gameId)?.View;
}
