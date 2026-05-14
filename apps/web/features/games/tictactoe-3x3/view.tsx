'use client';

import { useMemo } from 'react';
import type { GameView, GameViewProps } from '../types';
import { Board } from '../board';
import { TttBoardStateSchema, type TttBoardState } from './schema';

/**
 * Tic-Tac-Toe 3×3 web renderer. Owns the state shape (parsed from
 * `MatchDto.state`), the cell glyphs (✕/◯), and the move payload shape
 * (`{ cell: int }`) — all per-module contract with the matching API-side
 * `TicTacToe3x3GameModule` and `TicTacToeMoveParser`. The platform shell
 * never inspects any of it (CLAUDE.md §7 "Platform thinness").
 */

function parseTttState(state: string): TttBoardState {
  // `state` is a server-produced JSON blob the platform forwards opaquely
  // (MatchDto.state). Zod-parse rather than cast so a malformed payload
  // fails loudly instead of crashing somewhere deep in the renderer
  // (CLAUDE.md §6 "validate every external input").
  return TttBoardStateSchema.parse(JSON.parse(state));
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
