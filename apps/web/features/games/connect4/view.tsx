'use client';

import { useMemo } from 'react';
import type { GameView, GameViewProps } from '../types';

/**
 * Connect 4 web renderer. Owns the state shape (parsed from
 * `MatchDto.state`), the disc/ring iconography (per platform-and-games.md
 * §2.1 — red as a solid disc, yellow as a ring so the two are
 * distinguishable for the most common forms of color-blindness), and the
 * move payload shape (`{ column: int }`) — all per-module contract with
 * the API-side `Connect4GameModule` and `Connect4MoveParser`. The
 * platform shell never inspects any of it (CLAUDE.md §7 "Platform
 * thinness").
 *
 * Input model: column-drop. Seven full-height column buttons stack on top
 * of the visual grid; clicking one drops the caller's disc into that
 * column. The grid itself is decorative once a column is chosen — there
 * are no per-cell click targets (CLAUDE.md §7 composition rule: don't
 * force Connect 4 through the generic `Board` cell-click model).
 */
interface Coord {
  row: number;
  col: number;
}
interface Connect4BoardState {
  rows: number;
  cols: number;
  cells: readonly (string | null)[];
  lastMove?: Coord;
  winningLine?: readonly Coord[];
}

function parseConnect4State(state: string): Connect4BoardState {
  return JSON.parse(state) as Connect4BoardState;
}

function indexOf(state: Connect4BoardState, row: number, col: number): number {
  return row * state.cols + col;
}

function columnIsFull(state: Connect4BoardState, col: number): boolean {
  return state.cells[indexOf(state, 0, col)] !== null;
}

export const Connect4View: GameView = ({
  matchState,
  canPlay,
  matchEnded,
  onSubmitMove,
}: GameViewProps) => {
  const board = useMemo(() => parseConnect4State(matchState), [matchState]);

  const winningSet = useMemo(() => {
    const set = new Set<number>();
    if (board.winningLine) {
      for (const c of board.winningLine) set.add(indexOf(board, c.row, c.col));
    }
    return set;
  }, [board]);

  const lastMoveIndex =
    board.lastMove !== undefined ? indexOf(board, board.lastMove.row, board.lastMove.col) : -1;

  const interactable = canPlay && !matchEnded;

  return (
    <div className="c4">
      <div className="c4__columns" role="group" aria-label="connect 4 columns">
        {Array.from({ length: board.cols }, (_, col) => {
          const full = columnIsFull(board, col);
          return (
            <button
              key={col}
              type="button"
              className="c4__drop"
              disabled={!interactable || full}
              aria-label={`drop disc in column ${col + 1}`}
              onClick={() => onSubmitMove({ column: col })}
            >
              <span aria-hidden>▼</span>
            </button>
          );
        })}
      </div>
      <div
        className="c4__grid"
        style={{
          gridTemplateColumns: `repeat(${board.cols}, 1fr)`,
          gridTemplateRows: `repeat(${board.rows}, 1fr)`,
        }}
        role="grid"
        aria-label="connect 4 board"
      >
        {board.cells.map((side, i) => {
          const isLast = i === lastMoveIndex;
          const isWinning = winningSet.has(i);
          const className =
            'c4__cell' +
            (side ? ` c4__cell--${side}` : '') +
            (isLast ? ' c4__cell--last' : '') +
            (isWinning ? ' c4__cell--winning' : '');
          return (
            <div
              key={i}
              role="gridcell"
              aria-label={
                side ? `${side} disc` : `empty cell row ${Math.floor(i / board.cols) + 1}`
              }
              className={className}
            >
              {side ? <span className="c4__disc" /> : null}
            </div>
          );
        })}
      </div>
    </div>
  );
};
