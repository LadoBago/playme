"use client";

import { useMemo } from "react";
import { t, type Locale } from "@playme/shared";
import { useTranslator } from "@/lib/use-locale";
import type { GameView, GameViewProps } from "../types";
import { ReversiBoardStateSchema, type ReversiBoardState } from "./schema";

/**
 * Localised short side label ("Dark" / "Light") for the platform's player
 * card. Side vocab stays inside this module (CLAUDE.md §7 "Platform
 * thinness"); the platform calls through `GameModule.getSideLabel`.
 */
export function reversiSideLabel(side: string, locale: Locale): string | null {
  if (side === "dark") return t("games.reversi.shortSideDark", locale);
  if (side === "light") return t("games.reversi.shortSideLight", locale);
  return null;
}

/**
 * Reversi web renderer. Owns the state shape (parsed from `MatchDto.state`),
 * the disc iconography, the click model (per-cell), and the legal-move
 * highlighting — all per-module contract with the API-side
 * `ReversiGameModule` and `ReversiMoveParser`. The platform shell never
 * inspects any of it (CLAUDE.md §7 "Platform thinness").
 *
 * Forced skips are resolved entirely server-side: when a placement strands
 * the opponent, the module keeps the mover on turn (`MoveResult.KeepTurn`,
 * seam B) and publishes `skippedSide` on the state. The renderer only
 * shows the explanatory toast — it never emits a move on the player's
 * behalf.
 */

const BOARD_SIZE_FOR_OPENING = 4;

const DIRECTIONS: readonly (readonly [number, number])[] = [
  [-1, -1],
  [-1, 0],
  [-1, 1],
  [0, -1],
  [0, 1],
  [1, -1],
  [1, 0],
  [1, 1],
];

function parseReversiState(state: string): ReversiBoardState {
  // Zod-parse the server-produced JSON blob so a malformed payload fails
  // loudly rather than corrupting rendering (CLAUDE.md §6 "validate every
  // external input").
  return ReversiBoardStateSchema.parse(JSON.parse(state));
}

function indexOf(size: number, row: number, col: number): number {
  return row * size + col;
}

function inBounds(size: number, r: number, c: number): boolean {
  return r >= 0 && r < size && c >= 0 && c < size;
}

/** Bounded array access (`Array.prototype.at`) — bypasses the security
 *  lint's bracket-access heuristic without disabling the rule wholesale. */
function cellAt(cells: readonly (string | null)[], i: number): string | null {
  return cells.at(i) ?? null;
}

/**
 * Compute the legal-placement set for `side` on `state`. Mirrors the
 * server's algorithm (`ReversiGameModule.HasAnyLegalMove`) so the renderer
 * can highlight legal cells without a round-trip. The server re-validates
 * every submitted move, so a client-side bug here surfaces as a server
 * rejection — not a divergent game state (CLAUDE.md §7).
 */
function legalPlacements(state: ReversiBoardState, side: string): Set<number> {
  const set = new Set<number>();
  const size = state.size;
  if (state.moveCount < BOARD_SIZE_FOR_OPENING) {
    for (let r = 3; r <= 4; r++) {
      for (let c = 3; c <= 4; c++) {
        const i = indexOf(size, r, c);
        if (cellAt(state.cells, i) === null) set.add(i);
      }
    }
    return set;
  }
  const other = side === "dark" ? "light" : "dark";
  for (let r = 0; r < size; r++) {
    for (let c = 0; c < size; c++) {
      const i = indexOf(size, r, c);
      if (cellAt(state.cells, i) !== null) continue;
      for (const [dr, dc] of DIRECTIONS) {
        let rr = r + dr;
        let cc = c + dc;
        let captured = 0;
        while (
          inBounds(size, rr, cc) &&
          cellAt(state.cells, indexOf(size, rr, cc)) === other
        ) {
          captured++;
          rr += dr;
          cc += dc;
        }
        if (
          captured > 0 &&
          inBounds(size, rr, cc) &&
          cellAt(state.cells, indexOf(size, rr, cc)) === side
        ) {
          set.add(i);
          break;
        }
      }
    }
  }
  return set;
}

export const ReversiView: GameView = ({
  matchState,
  callerSide,
  canPlay,
  matchEnded,
  onSubmitMove,
}: GameViewProps) => {
  const { t, tf } = useTranslator();
  const board = useMemo(() => parseReversiState(matchState), [matchState]);

  const callerLegal = useMemo(() => {
    if (callerSide === null || !canPlay || matchEnded) return new Set<number>();
    return legalPlacements(board, callerSide);
  }, [board, callerSide, canPlay, matchEnded]);

  const lastPlacementIndex = board.lastPlacement
    ? indexOf(board.size, board.lastPlacement.row, board.lastPlacement.col)
    : -1;

  const flippedSet = useMemo(() => {
    const set = new Set<number>();
    for (const coord of board.flippedLastTurn ?? []) {
      set.add(indexOf(board.size, coord.row, coord.col));
    }
    return set;
  }, [board.flippedLastTurn, board.size]);

  const interactable = canPlay && !matchEnded && callerSide !== null;

  function handleCellClick(cell: number) {
    if (!interactable) return;
    if (!callerLegal.has(cell)) return;
    const row = Math.floor(cell / board.size);
    const col = cell % board.size;
    onSubmitMove({ row, col });
  }

  // `skippedSide` names the player whose turn the server skipped — from
  // their seat the opponent (the mover) plays again, so the skipped viewer
  // gets the "self" copy and the mover gets the "opponent" copy. Same
  // direction as the retired `lastPassSide === callerSide` ternary; don't
  // "fix" it backwards.
  const passToastKey = board.skippedSide
    ? board.skippedSide === callerSide
      ? "games.reversi.toast.autoPassSelf"
      : "games.reversi.toast.autoPassOpponent"
    : null;

  const cellButtons = board.cells.map((side, i) => {
    const row = Math.floor(i / board.size) + 1;
    const col = (i % board.size) + 1;
    const isLegal = interactable && callerLegal.has(i);
    const isLast = i === lastPlacementIndex;
    const isFlipped = flippedSet.has(i);
    const className =
      "rv__cell" +
      (side ? ` rv__cell--${side}` : "") +
      (isLegal ? " rv__cell--legal" : "") +
      (isLast ? " rv__cell--last" : "") +
      (isFlipped ? " rv__cell--flipped" : "");
    const cellLabel =
      side === "dark"
        ? t("games.reversi.cell.discDark")
        : side === "light"
          ? t("games.reversi.cell.discLight")
          : isLegal
            ? tf("games.reversi.cell.legal", { row, col })
            : tf("games.reversi.cell.empty", { row, col });
    return (
      <button
        key={i}
        type="button"
        role="gridcell"
        className={className}
        disabled={!interactable || side !== null || !isLegal}
        aria-label={cellLabel}
        onClick={() => handleCellClick(i)}
      >
        {side ? <span className="rv__disc" /> : null}
      </button>
    );
  });

  return (
    <div className="rv">
      {passToastKey && (
        <div role="status" className="rv__toast" aria-live="polite">
          {t(passToastKey)}
        </div>
      )}
      <div
        className="rv__grid"
        style={{
          gridTemplateColumns: `repeat(${board.size}, var(--rv-cell))`,
          gridTemplateRows: `repeat(${board.size}, var(--rv-cell))`,
        }}
        role="grid"
        aria-label={t("games.reversi.board.label")}
      >
        {/* role="grid" requires row children (WCAG 1.3.1); display: contents
            keeps the cells as direct grid items so the track layout is
            unchanged. */}
        {Array.from({ length: board.size }, (_, r) => (
          <div key={r} role="row" className="rv__row">
            {cellButtons.slice(r * board.size, (r + 1) * board.size)}
          </div>
        ))}
      </div>
      <div className="rv__counters" role="status" aria-live="polite">
        <span className="rv__counter rv__counter--dark">
          {tf("games.reversi.score.dark", { count: board.darkCount })}
        </span>
        <span className="rv__counter rv__counter--light">
          {tf("games.reversi.score.light", { count: board.lightCount })}
        </span>
      </div>
    </div>
  );
};
