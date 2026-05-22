'use client';

import { useMemo } from 'react';
import { t, type Locale } from '@playme/shared';
import type { GameView, GameViewProps } from '../types';
import { Board } from '../board';
import { TicTacToeBoardStateSchema, type TicTacToeBoardState } from './schema';

/**
 * Localised short side label ("X" / "O") for the platform's player card.
 * Side vocab stays inside this module (CLAUDE.md §7 "Platform thinness");
 * the platform calls through `GameModule.getSideLabel`. Shared with the
 * legacy per-size renderers via the same `games.tictactoe.shortSide*` keys.
 */
export function tictactoeSideLabel(side: string, locale: Locale): string | null {
  if (side === 'x') return t('games.tictactoe.shortSideX', locale);
  if (side === 'o') return t('games.tictactoe.shortSideO', locale);
  return null;
}

/**
 * Unified Tic-Tac-Toe web renderer (Sprint 9 PR2). Owns the state shape
 * (parsed from `MatchDto.state`), the cell glyphs (✕/◯), and the move
 * payload shape (`{ cell: int }`) — all per-module contract with the
 * matching API-side `TicTacToeGameModule` and `TicTacToeMoveParser`. The
 * board grows or shrinks per the host-chosen `boardSize` carried in
 * `gameOptions` and reflected in the state's `rows`/`cols`/`winLength`
 * fields.
 */

function parseTicTacToeState(state: string): TicTacToeBoardState {
  // `state` is a server-produced JSON blob the platform forwards opaquely
  // (MatchDto.state). Zod-parse rather than cast so a malformed payload
  // fails loudly instead of crashing somewhere deep in the renderer
  // (CLAUDE.md §6 "validate every external input").
  return TicTacToeBoardStateSchema.parse(JSON.parse(state));
}

function renderTicTacToeCell(side: string | null): string {
  if (side === null) return '';
  if (side === 'x') return '✕';
  if (side === 'o') return '◯';
  return side.toUpperCase();
}

export const TicTacToeView: GameView = ({
  matchState,
  callerSide,
  canPlay,
  matchEnded,
  onSubmitMove,
}: GameViewProps) => {
  const board = useMemo(() => parseTicTacToeState(matchState), [matchState]);

  const winningCells = useMemo(() => {
    const set = new Set<number>();
    if (board.winningLine) {
      for (const c of board.winningLine) set.add(c.row * board.cols + c.col);
    }
    return set;
  }, [board.cols, board.winningLine]);

  const winningSide = useMemo(() => {
    if (!board.winningLine || board.winningLine.length === 0) return null;
    const first = board.winningLine[0]!;
    return board.cells[first.row * board.cols + first.col] ?? null;
  }, [board.cells, board.cols, board.winningLine]);
  const winningIsLoss = winningSide !== null && callerSide !== null && winningSide !== callerSide;

  return (
    <Board
      rows={board.rows}
      cols={board.cols}
      cells={board.cells}
      lastMoveCell={board.lastMove ?? null}
      winningCells={winningCells}
      winningIsLoss={winningIsLoss}
      canPlay={canPlay && !matchEnded}
      onCellClick={(cell) => onSubmitMove({ cell })}
      renderCell={renderTicTacToeCell}
    />
  );
};
