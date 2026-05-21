'use client';

import type { CSSProperties, ReactNode } from 'react';
import { useTranslator } from '@/lib/use-locale';

interface BoardProps {
  rows: number;
  cols: number;
  cells: readonly (string | null)[];
  /** Index of the most-recently-played cell, or null if none. */
  lastMoveCell: number | null;
  /** Flat indices of the winning line, if a Win has occurred. */
  winningCells: ReadonlySet<number>;
  /** True when the caller is the losing side of the winning line — the
   *  highlight switches from the win tokens to the brand-red lose tokens
   *  so the winning row reads as "you lose" instead of "you win". */
  winningIsLoss?: boolean;
  canPlay: boolean;
  onCellClick: (cell: number) => void;
  /**
   * Per-game cell renderer. Called for every cell with its side string (or
   * null for empty). The Board never inspects side vocabulary — per-game
   * iconography belongs to the per-game module (CLAUDE.md §7 "Platform
   * thinness").
   */
  renderCell: (side: string | null) => ReactNode;
}

/**
 * Generic grid display. Game modules choose to use it (composition, per
 * CLAUDE.md §7 "Platform thinness") — the platform room shell does not
 * import it. The cell glyph is injected via <see cref="BoardProps.renderCell"/>;
 * input handling (cell click vs. column drop, etc.) is the calling game's
 * responsibility, which may wrap or replace this component entirely.
 */
export function Board({
  rows,
  cols,
  cells,
  lastMoveCell,
  winningCells,
  winningIsLoss = false,
  canPlay,
  onCellClick,
  renderCell,
}: BoardProps) {
  const { t, tf } = useTranslator();
  return (
    <div
      className="board"
      style={
        {
          gridTemplateColumns: `repeat(${cols}, 1fr)`,
          gridTemplateRows: `repeat(${rows}, 1fr)`,
          '--board-cells': cols,
        } as CSSProperties
      }
      role="grid"
      aria-label={t('match.board.label')}
    >
      {cells.map((side, i) => {
        const filled = side !== null;
        const isLast = lastMoveCell === i;
        const isWinning = winningCells.has(i);
        const className =
          'board__cell' +
          (filled ? ' board__cell--filled' : '') +
          (isLast ? ' board__cell--last' : '') +
          (isWinning ? ' board__cell--winning' : '') +
          (isWinning && winningIsLoss ? ' board__cell--winning-lost' : '');

        return (
          <button
            key={i}
            type="button"
            className={className}
            disabled={!canPlay || filled}
            aria-label={tf('match.board.cell.label', {
              row: Math.floor(i / cols) + 1,
              col: (i % cols) + 1,
            })}
            onClick={() => onCellClick(i)}
          >
            {renderCell(side)}
          </button>
        );
      })}
    </div>
  );
}
