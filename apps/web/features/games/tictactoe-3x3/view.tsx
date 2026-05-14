'use client';

import { useMemo } from 'react';
import type { GameView, GameViewProps } from '../types';
import { Board } from '../board';

/**
 * Tic-Tac-Toe 3×3 web renderer. Owns the state shape (parsed from
 * `MatchDto.state`), the cell glyphs (✕/◯), and the move payload shape
 * (`{ cell: int }`) — all per-module contract with the matching API-side
 * `TicTacToe3x3GameModule` and `TicTacToeMoveParser`. The platform shell
 * never inspects any of it (CLAUDE.md §7 "Platform thinness").
 */
interface TttBoardState {
  rows: number;
  cols: number;
  cells: readonly (string | null)[];
  /** Cell index of the most-recently-played move, if any. */
  lastMove?: number;
  /** Cells aligned by the winning move, if the match is won. */
  winningLine?: readonly { row: number; col: number }[];
}

function parseTttState(state: string): TttBoardState {
  return JSON.parse(state) as TttBoardState;
}

function renderTttCell(side: string | null): string {
  if (side === null) return '';
  if (side === 'x') return '✕';
  if (side === 'o') return '◯';
  return side.toUpperCase();
}

export const TicTacToe3x3View: GameView = ({
  matchState,
  canPlay,
  matchEnded,
  onSubmitMove,
}: GameViewProps) => {
  const board = useMemo(() => parseTttState(matchState), [matchState]);

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
      renderCell={renderTttCell}
    />
  );
};
