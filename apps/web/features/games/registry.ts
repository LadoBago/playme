import dynamic from 'next/dynamic';
import type { GameModule } from './types';
import { connect4SideLabel } from './connect4/side-label';
import { reversiSideLabel } from './reversi/side-label';
import { seabattleSideLabel } from './seabattle/side-label';
import { tictactoeSideLabel } from './tictactoe/side-label';

/**
 * Game-id → module. Adding a new game is one line here plus a new
 * `features/games/<game>/view.tsx` (+ `side-label.ts`) — zero edits in the
 * platform room shell (CLAUDE.md §7 "Platform thinness"). Per-game
 * vocabulary ("x"/"o", "red"/"yellow", "dark"/"light") stays inside the
 * module: the platform only ever sees the `GameModule` shape.
 *
 * Views (and Sea Battle's TurnStatusExtra, which shares the view module's
 * parsing code) load through `next/dynamic`, so a room ships only the
 * active game's renderer instead of all four. Side labels stay eager:
 * the match header resolves them synchronously on first paint, and they
 * are a few lines each.
 */
const MODULES = new Map<string, GameModule>([
  [
    'tictactoe',
    {
      View: dynamic(() => import('./tictactoe/view').then((m) => m.TicTacToeView)),
      getSideLabel: tictactoeSideLabel,
    },
  ],
  [
    'connect4',
    {
      View: dynamic(() => import('./connect4/view').then((m) => m.Connect4View)),
      getSideLabel: connect4SideLabel,
    },
  ],
  [
    'reversi',
    {
      View: dynamic(() => import('./reversi/view').then((m) => m.ReversiView)),
      getSideLabel: reversiSideLabel,
    },
  ],
  [
    'seabattle',
    {
      View: dynamic(() => import('./seabattle/view').then((m) => m.SeaBattleView)),
      getSideLabel: seabattleSideLabel,
      TurnStatusExtra: dynamic(() =>
        import('./seabattle/view').then((m) => m.SeaBattleTurnStatus),
      ),
    },
  ],
]);

export function findGameModule(gameId: string): GameModule | undefined {
  return MODULES.get(gameId);
}
