'use client';

import { useMemo } from 'react';
import { t, type Locale } from '@playme/shared';
import type { GameView, GameViewProps } from '../types';
import { Board } from '../board';
import { Ttt6x6BoardStateSchema, type Ttt6x6BoardState } from './schema';

/**
 * Localised short side label ("X" / "O") for the platform's player card.
 * Side vocab stays inside this module (CLAUDE.md §7 "Platform thinness");
 * the platform calls through `GameModule.getSideLabel`. Reuses the shared
 * `games.tictactoe.shortSideX/O` i18n keys because every TTT variant shows
 * the same X/O glyphs — acceptable composition within the TTT family
 * (the per-game *vocab constants* are still independent on the API side).
 */
export function tictactoe6x6SideLabel(side: string, locale: Locale): string | null {
  if (side === 'x') return t('games.tictactoe.shortSideX', locale);
  if (side === 'o') return t('games.tictactoe.shortSideO', locale);
  return null;
}

/**
 * Tic-Tac-Toe 6×6 web renderer. Owns the state shape (parsed from
 * `MatchDto.state`), the cell glyphs (✕/◯), and the move payload shape
 * (`{ cell: int }`) — all per-module contract with the matching API-side
 * `TicTacToe6x6GameModule` and `TicTacToe6x6MoveParser`. The platform
 * shell never inspects any of it (CLAUDE.md §7 "Platform thinness").
 *
 * Intentionally **not** importing from `../tictactoe-3x3/`; per-module
 * duplication is the contract — copying the renderer's shape keeps each
 * game independently complete.
 */

function parseTtt6x6State(state: string): Ttt6x6BoardState {
  // `state` is a server-produced JSON blob the platform forwards opaquely
  // (MatchDto.state). Zod-parse rather than cast so a malformed payload
  // fails loudly instead of crashing somewhere deep in the renderer
  // (CLAUDE.md §6 "validate every external input").
  return Ttt6x6BoardStateSchema.parse(JSON.parse(state));
}

function renderTtt6x6Cell(side: string | null): string {
  if (side === null) return '';
  if (side === 'x') return '✕';
  if (side === 'o') return '◯';
  return side.toUpperCase();
}

export const TicTacToe6x6View: GameView = ({
  matchState,
  canPlay,
  matchEnded,
  onSubmitMove,
}: GameViewProps) => {
  const board = useMemo(() => parseTtt6x6State(matchState), [matchState]);

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
      renderCell={renderTtt6x6Cell}
    />
  );
};
