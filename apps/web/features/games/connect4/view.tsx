'use client';

import { useMemo } from 'react';
import { t, tf } from '@playme/shared';
import type { GameView, GameViewProps } from '../types';
import { Connect4BoardStateSchema, type Connect4BoardState } from './schema';

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
 * Input model: column-drop. Seven column buttons stack above the grid
 * and another set below it; tapping either row drops the caller's disc
 * into that column. The mirrored bottom row is for one-handed mobile use
 * (the top row is out of the thumb zone on a phone held normally). The
 * grid itself is decorative — there are no per-cell click targets
 * (CLAUDE.md §7 composition rule: don't force Connect 4 through the
 * generic `Board` cell-click model).
 */

function parseConnect4State(state: string): Connect4BoardState {
  // Zod-parse the server-produced JSON blob (MatchDto.state) so a
  // malformed payload fails loudly at the boundary rather than corrupting
  // rendering (CLAUDE.md §6 "validate every external input").
  return Connect4BoardStateSchema.parse(JSON.parse(state));
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

  const dropRow = (placement: 'top' | 'bottom') => (
    <div
      className="c4__columns"
      role="group"
      aria-label={t(
        placement === 'top'
          ? 'games.connect4.columns.top'
          : 'games.connect4.columns.bottom',
      )}
    >
      {Array.from({ length: board.cols }, (_, col) => {
        const full = columnIsFull(board, col);
        return (
          <button
            key={col}
            type="button"
            className="c4__drop"
            disabled={!interactable || full}
            aria-label={tf('games.connect4.dropColumn', { col: col + 1 })}
            onClick={() => onSubmitMove({ column: col })}
          >
            <span aria-hidden>{placement === 'top' ? '▼' : '▲'}</span>
          </button>
        );
      })}
    </div>
  );

  return (
    <div className="c4">
      {dropRow('top')}
      <div
        className="c4__grid"
        style={{
          gridTemplateColumns: `repeat(${board.cols}, 1fr)`,
          gridTemplateRows: `repeat(${board.rows}, 1fr)`,
        }}
        role="grid"
        aria-label={t('games.connect4.board.label')}
      >
        {board.cells.map((side, i) => {
          const isLast = i === lastMoveIndex;
          const isWinning = winningSet.has(i);
          const className =
            'c4__cell' +
            (side ? ` c4__cell--${side}` : '') +
            (isLast ? ' c4__cell--last' : '') +
            (isWinning ? ' c4__cell--winning' : '');
          // Side identifiers ("red"/"yellow") are this module's vocab and
          // stay inside it (CLAUDE.md §7 "Platform thinness"); the inline
          // branch resolves them to localised cell labels.
          const cellLabel = side
            ? side === 'red'
              ? t('games.connect4.cell.discRed')
              : t('games.connect4.cell.discYellow')
            : tf('games.connect4.cell.empty', { row: Math.floor(i / board.cols) + 1 });
          return (
            <div
              key={i}
              role="gridcell"
              aria-label={cellLabel}
              className={className}
            >
              {side ? <span className="c4__disc" /> : null}
            </div>
          );
        })}
      </div>
      {dropRow('bottom')}
    </div>
  );
};
