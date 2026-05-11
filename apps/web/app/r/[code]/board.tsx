'use client';

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
}

/**
 * Generic grid board. Same renderer works for every Sprint 1 game; per-
 * game iconography (X/O vs disc/ring for Connect 4) is just a function
 * of the side string.
 */
export function Board({
  rows,
  cols,
  cells,
  lastMoveCell,
  winningCells,
  canPlay,
  onCellClick,
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
            {renderSide(side)}
          </button>
        );
      })}
    </div>
  );
}

function renderSide(side: string | null): string {
  if (side === null) return '';
  // Sprint 1 only supports Tic-Tac-Toe; Connect 4's red/yellow rendering
  // ships in Sprint 3.
  if (side === 'x') return '✕';
  if (side === 'o') return '◯';
  return side.toUpperCase();
}
