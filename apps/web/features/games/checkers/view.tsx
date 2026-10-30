'use client';

import { useMemo, useState } from 'react';
import { useTranslator } from '@/lib/use-locale';
import type { GameView, GameViewProps, TurnStatusExtraProps } from '../types';
import {
  BOARD_SIZE,
  CheckersStateSchema,
  coordIndex,
  isDarkSquare,
  isKing,
  sideOf,
  type CheckersMoveInfo,
  type CheckersState,
} from './schema';

/**
 * Checkers (Russian draughts) web renderer. Owns the state shape (parsed
 * from `MatchDto.state`), the piece iconography, the two-tap click model
 * (piece, then destination), and the highlighting — all per-module
 * contract with the API-side `CheckersGameModule` and `CheckersMoveParser`.
 * The platform shell never inspects any of it (CLAUDE.md §7 "Platform
 * thinness").
 *
 * The renderer implements no rules: every highlight comes from the
 * server-published `legalMoves`, which also makes mandatory capture
 * legible (only capturing pieces light up) and keeps a multi-jump chain
 * on rails (only the chaining piece has moves). Each jump is one move;
 * the server keeps the turn (`MoveResult.KeepTurn`) while the chain is
 * open and the state's `chain` records the obligation.
 */

function parseCheckersState(state: string): CheckersState {
  // Zod-parse the server-produced JSON blob so a malformed payload fails
  // loudly rather than corrupting rendering (CLAUDE.md §6 "validate every
  // external input").
  return CheckersStateSchema.parse(JSON.parse(state));
}

/** Bounded array access (`Array.prototype.at`) — bypasses the security
 *  lint's bracket-access heuristic without disabling the rule wholesale. */
function cellAt(cells: CheckersState['cells'], i: number): CheckersState['cells'][number] {
  return cells.at(i) ?? null;
}

/**
 * Inline chain feedback for the room shell's turn-status row
 * (`GameModule.TurnStatusExtra`): while a multi-capture chain is open the
 * mover is told to keep capturing and the opponent that the chain is in
 * progress — explains why the turn pill didn't flip after a jump.
 */
export function CheckersTurnStatus({ matchState, canPlay }: TurnStatusExtraProps) {
  const { t } = useTranslator();
  const chain = useMemo(() => {
    try {
      return parseCheckersState(matchState).chain ?? null;
    } catch {
      return null;
    }
  }, [matchState]);
  if (!chain) return null;
  return (
    <span className="ck__status" role="status">
      {t(canPlay ? 'games.checkers.chain.self' : 'games.checkers.chain.opponent')}
    </span>
  );
}

export const CheckersView: GameView = ({
  matchState,
  callerSide,
  canPlay,
  matchEnded,
  onSubmitMove,
}: GameViewProps) => {
  const { t, tf } = useTranslator();
  const board = useMemo(() => parseCheckersState(matchState), [matchState]);
  const interactable = canPlay && !matchEnded && callerSide !== null;

  // Movable pieces and, per piece, its destinations — straight from the
  // server's list. Empty when it isn't the caller's turn.
  const movable = useMemo(() => {
    const map = new Map<number, CheckersMoveInfo[]>();
    if (!interactable) return map;
    for (const move of board.legalMoves) {
      const from = coordIndex(move.from);
      const list = map.get(from) ?? [];
      list.push(move);
      map.set(from, list);
    }
    return map;
  }, [board.legalMoves, interactable]);

  // Two-tap model: the selection is a cell index the player tapped; it is
  // only honoured while that piece is still movable in the current state,
  // so a new server state never leaves a stale selection behind. When a
  // single piece can move (every mid-chain state, forced single captures)
  // it is pre-selected so the destinations show without an extra tap.
  const [tapped, setTapped] = useState<number | null>(null);
  const selected =
    tapped !== null && movable.has(tapped)
      ? tapped
      : movable.size === 1
        ? (movable.keys().next().value ?? null)
        : null;
  const targets = useMemo(() => {
    const map = new Map<number, CheckersMoveInfo>();
    if (selected === null) return map;
    for (const move of movable.get(selected) ?? []) {
      map.set(coordIndex(move.to), move);
    }
    return map;
  }, [movable, selected]);

  const pending = useMemo(() => {
    const set = new Set<number>();
    for (const c of board.chain?.captured ?? []) set.add(coordIndex(c));
    return set;
  }, [board.chain]);

  const lastTo = board.lastMove ? coordIndex(board.lastMove.to) : -1;
  const lastFrom = board.lastMove ? coordIndex(board.lastMove.from) : -1;

  // A decided win leaves the side to move without a legal move; crown the
  // other side's counter. Resign/timeout endings aren't derivable from the
  // state and get no crown here — the platform banner names the winner.
  const winner =
    matchEnded && !board.drawReason && board.legalMoves.length === 0
      ? board.sideToMove === 'white'
        ? 'black'
        : 'white'
      : null;

  function handleCellClick(index: number) {
    if (!interactable) return;
    const target = targets.get(index);
    if (target) {
      setTapped(null);
      onSubmitMove({ from: target.from, to: target.to });
      return;
    }
    if (movable.has(index)) setTapped(index);
  }

  // Each seat sees its own pieces at the bottom: the black seat views the
  // board rotated 180° (spectators and white see white at the bottom).
  // Rotation is display-only — indices submitted to the server are the
  // server's row-major indices.
  const flipped = callerSide === 'black';
  const displayOrder = useMemo(() => {
    const order: number[] = [];
    for (let r = 0; r < BOARD_SIZE; r++) {
      for (let c = 0; c < BOARD_SIZE; c++) {
        const row = flipped ? BOARD_SIZE - 1 - r : r;
        const col = flipped ? BOARD_SIZE - 1 - c : c;
        order.push(row * BOARD_SIZE + col);
      }
    }
    return order;
  }, [flipped]);

  const cellButtons = displayOrder.map((i) => {
    const row = Math.floor(i / BOARD_SIZE);
    const col = i % BOARD_SIZE;
    const piece = cellAt(board.cells, i);
    const owner = sideOf(piece);
    const king = isKing(piece);
    const dark = isDarkSquare(row, col);
    const isMovable = movable.has(i);
    const isSelected = selected === i;
    const target = targets.get(i);
    const isPending = pending.has(i);
    const clickable = interactable && (isMovable || target !== undefined);

    const className =
      'ck__cell' +
      (dark ? ' ck__cell--dark' : ' ck__cell--light') +
      (isMovable ? ' ck__cell--movable' : '') +
      (isSelected ? ' ck__cell--selected' : '') +
      (target ? (target.captured ? ' ck__cell--target ck__cell--capture' : ' ck__cell--target') : '') +
      (isPending ? ' ck__cell--pending' : '') +
      (i === lastTo ? ' ck__cell--last' : '') +
      (i === lastFrom ? ' ck__cell--last-from' : '');

    const pos = { row: row + 1, col: col + 1 };
    const label = piece
      ? tf(
          owner === 'white'
            ? king
              ? 'games.checkers.cell.kingWhite'
              : 'games.checkers.cell.manWhite'
            : king
              ? 'games.checkers.cell.kingBlack'
              : 'games.checkers.cell.manBlack',
          pos,
        )
      : target
        ? tf(target.captured ? 'games.checkers.cell.capture' : 'games.checkers.cell.target', pos)
        : tf('games.checkers.cell.empty', pos);

    return (
      <button
        key={i}
        type="button"
        role="gridcell"
        className={className}
        disabled={!clickable}
        aria-label={label}
        aria-pressed={isMovable ? isSelected : undefined}
        onClick={() => handleCellClick(i)}
      >
        {piece ? (
          <span className={`ck__piece ck__piece--${owner ?? 'white'}${king ? ' ck__piece--king' : ''}`}>
            {king ? <CrownGlyph /> : null}
          </span>
        ) : null}
      </button>
    );
  });

  return (
    <div className="ck">
      <div
        className={`ck__grid${matchEnded ? ' ck__grid--ended' : ''}`}
        role="grid"
        aria-label={t('games.checkers.board.label')}
      >
        {/* role="grid" requires row children (WCAG 1.3.1); display: contents
            keeps the cells as direct grid items so the track layout is
            unchanged. */}
        {Array.from({ length: BOARD_SIZE }, (_, r) => (
          <div key={r} role="row" className="ck__row">
            {cellButtons.slice(r * BOARD_SIZE, (r + 1) * BOARD_SIZE)}
          </div>
        ))}
      </div>
      <div className="ck__counters" role="status" aria-live="polite">
        <span className={`ck__counter ck__counter--white${winner === 'white' ? ' ck__counter--winner' : ''}`}>
          {tf('games.checkers.score.white', { count: board.whiteCount })}
        </span>
        <span className={`ck__counter ck__counter--black${winner === 'black' ? ' ck__counter--winner' : ''}`}>
          {tf('games.checkers.score.black', { count: board.blackCount })}
        </span>
      </div>
      {matchEnded && board.drawReason ? (
        <p className="ck__draw-reason" role="status">
          {t(`games.checkers.draw.${board.drawReason}`)}
        </p>
      ) : null}
    </div>
  );
};

/** Crown mark for kings — a shape cue, so kings read without relying on hue. */
function CrownGlyph() {
  return (
    <svg className="ck__crown" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      <path d="M4 17h16v2H4zm0-2 1.5-8L10 11l2-6 2 6 4.5-4L20 15z" />
    </svg>
  );
}
