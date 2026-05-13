'use client';

import type { ReactNode } from 'react';

interface BoardProps {
  rows: number;
  cols: number;
  cells: readonly (string | null)[];
  /** Index of the most-recently-played cell, or null if none. */
  lastMoveCell: number | null;
  /** Flat indices of the winning line, if a Win has occurred. */
  winningCells: ReadonlySet<number>;
  canPlay: boolean;
  onCellClick: (cell: number) => void;
  /**
   * Per-game cell renderer. Called for every cell with its side string (or
   * null for empty). The platform Board component never inspects side
   * vocabulary — per-game iconography belongs to the per-game module
   * (CLAUDE.md §7 "Platform thinness").
   */
  renderCell: (side: string | null) => ReactNode;
}

/**
 * Generic grid board. Same renderer works for every grid-shaped game; the
 * per-game iconography (X/O vs disc/ring for Connect 4) is injected via
 * <see cref="BoardProps.renderCell"/>.
 */
export function Board({
  rows,
  cols,
  cells,
  lastMoveCell,
  winningCells,
  canPlay,
  onCellClick,
  renderCell,
}: BoardProps) {
  return (
    <div
      className="board"
      style={{
        gridTemplateColumns: `repeat(${cols}, 1fr)`,
        gridTemplateRows: `repeat(${rows}, 1fr)`,
      }}
      role="grid"
      aria-label="game board"
    >
      {cells.map((side, i) => {
        const filled = side !== null;
        const isLast = lastMoveCell === i;
        const isWinning = winningCells.has(i);
        const className =
          'board__cell' +
          (filled ? ' board__cell--filled' : '') +
          (isLast ? ' board__cell--last' : '') +
          (isWinning ? ' board__cell--winning' : '');

        return (
          <button
            key={i}
            type="button"
            className={className}
            disabled={!canPlay || filled}
            aria-label={`row ${Math.floor(i / cols) + 1} column ${(i % cols) + 1}`}
            onClick={() => onCellClick(i)}
          >
            {renderCell(side)}
          </button>
        );
      })}
    </div>
  );
}
