'use client';

import { useMemo } from 'react';
import { t, type Locale } from '@playme/shared';
import type { GameView, GameViewProps } from '../types';
import { Board } from '../board';
import { Ttt9x9BoardStateSchema, type Ttt9x9BoardState } from './schema';

/**
 * Localised short side label ("X" / "O") for the platform's player card.
 * Side vocab stays inside this module (CLAUDE.md §7 "Platform thinness");
 * the platform calls through `GameModule.getSideLabel`. Reuses the shared
 * `games.tictactoe.shortSide*` keys — the TTT family agrees on X/O
 * vocabulary; that's acceptable composition within the family, not a
 * platform-level shared concept.
 */
export function tictactoe9x9SideLabel(side: string, locale: Locale): string | null {
  if (side === 'x') return t('games.tictactoe.shortSideX', locale);
  if (side === 'o') return t('games.tictactoe.shortSideO', locale);
  return null;
}

/**
 * Tic-Tac-Toe 9×9 web renderer. Owns the state shape (parsed from
 * `MatchDto.state`), the cell glyphs (✕/◯), and the move payload shape
 * (`{ cell: int }`) — all per-module contract with the matching API-side
 * `TicTacToe9x9GameModule` and `TicTacToe9x9MoveParser`. The platform shell
 * never inspects any of it (CLAUDE.md §7 "Platform thinness"). Independent
 * of (and intentionally not shared with) the `tictactoe-3x3` renderer.
 */

function parseTtt9x9State(state: string): Ttt9x9BoardState {
  // `state` is a server-produced JSON blob the platform forwards opaquely
  // (MatchDto.state). Zod-parse rather than cast so a malformed payload
  // fails loudly instead of crashing somewhere deep in the renderer
  // (CLAUDE.md §6 "validate every external input").
  return Ttt9x9BoardStateSchema.parse(JSON.parse(state));
}

function renderTtt9x9Cell(side: string | null): string {
  if (side === null) return '';
  if (side === 'x') return '✕';
  if (side === 'o') return '◯';
  return side.toUpperCase();
}

export const TicTacToe9x9View: GameView = ({
  matchState,
  canPlay,
  matchEnded,
  onSubmitMove,
}: GameViewProps) => {
  const board = useMemo(() => parseTtt9x9State(matchState), [matchState]);

  const winningCells = useMemo(() => {
    const set = new Set<number>();
    if (board.winningLine) {
      for (const c of board.winningLine) set.add(c.row * board.cols + c.col);
    }
    return set;
  }, [board.cols, board.winningLine]);

  return (
    <Board
      rows={board.rows}
      cols={board.cols}
      cells={board.cells}
      lastMoveCell={board.lastMove ?? null}
      winningCells={winningCells}
      canPlay={canPlay && !matchEnded}
      onCellClick={(cell) => onSubmitMove({ cell })}
      renderCell={renderTtt9x9Cell}
    />
  );
};
