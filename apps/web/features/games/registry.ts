import type { GameModule, GameView } from './types';
import { Connect4View, connect4SideLabel } from './connect4/view';
import { ReversiView, reversiSideLabel } from './reversi/view';
import { TicTacToe3x3View, tttSideLabel } from './tictactoe-3x3/view';
import { TicTacToe6x6View, tictactoe6x6SideLabel } from './tictactoe-6x6/view';
import { TicTacToe9x9View, tictactoe9x9SideLabel } from './tictactoe-9x9/view';

/**
 * Game-id → module. Adding a new game is one line here plus a new
 * `features/games/<game>/view.tsx` — zero edits in the platform room
 * shell (CLAUDE.md §7 "Platform thinness"). Per-game vocabulary ("x"/"o",
 * "red"/"yellow", "dark"/"light") stays inside the module: the platform
 * only ever sees the `GameModule` shape.
 */
const MODULES = new Map<string, GameModule>([
  ['tictactoe-3x3', { View: TicTacToe3x3View, getSideLabel: tttSideLabel }],
  ['tictactoe-6x6', { View: TicTacToe6x6View, getSideLabel: tictactoe6x6SideLabel }],
  ['tictactoe-9x9', { View: TicTacToe9x9View, getSideLabel: tictactoe9x9SideLabel }],
  ['connect4', { View: Connect4View, getSideLabel: connect4SideLabel }],
  ['reversi', { View: ReversiView, getSideLabel: reversiSideLabel }],
]);

export function findGameModule(gameId: string): GameModule | undefined {
  return MODULES.get(gameId);
}

export function findGameView(gameId: string): GameView | undefined {
  return MODULES.get(gameId)?.View;
}
