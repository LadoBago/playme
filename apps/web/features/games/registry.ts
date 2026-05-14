import type { GameView } from './types';
import { Connect4View } from './connect4/view';
import { TicTacToe3x3View } from './tictactoe-3x3/view';

/**
 * Game-id → web renderer. Adding a new game (Sprints 3–4) is one line here
 * plus a new `features/games/<game>/view.tsx` — zero edits in the platform
 * room shell (CLAUDE.md §7 "Platform thinness").
 */
const VIEWS = new Map<string, GameView>([
  ['tictactoe-3x3', TicTacToe3x3View],
  ['connect4', Connect4View],
]);

export function findGameView(gameId: string): GameView | undefined {
  return VIEWS.get(gameId);
}
