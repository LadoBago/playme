# Checkers (`checkers`)

> **Status: planned — not yet implemented.** Sprint plan: [`roadmap/sprint-11-checkers.md`](../roadmap/sprint-11-checkers.md). This file is the canonical rules spec the implementation must match; see [`platform.md`](../platform.md) §2 for the catalog and the rules every module shares.

**Russian draughts** (шашки) — the ruleset played across Georgia and the post-Soviet space, matching the convention set by Sea Battle. **Not** English/American checkers (no backward captures, non-flying kings) and **not** International 10×10 draughts (majority-capture rule). A future Anglo-American variant, if wanted, is a `gameOptions` knob on this module per the Sprint 9 pattern, not a sibling module. Display names: ka **„შაში"**, en **"Checkers"**.

| | |
|---|---|
| **Board** | 8×8, play on the 32 dark squares only; a1 is dark, so dark squares satisfy `(row + col) % 2 == 1` with `row 0` at the top |
| **Sides** | **white** and **black** — white moves first. Black's men start on rows 0–2 (top), white's on rows 5–7 (bottom); rows 3–4 are empty |
| **Pieces** | 12 **men** per side; a man promotes to a **king** on the far rank |
| **Default clock budget** | 10 min per side ([`platform.md`](../platform.md) §1 #3) |
| **Game options** | none in v1 (`ValidateOptions` requires `null`) |
| **Platform seams used** | `IGameModule` + `MoveResult.KeepTurn` (multi-capture chains). Perfect information — no `IHiddenStateGame`, no `ISetupGame` |

## Rules

**Movement.**

- A **man** moves one square **diagonally forward** to an empty square (white toward row 0, black toward row 7).
- A **king** (flying king) moves any number of squares along a diagonal through empty squares, in any direction.

**Capturing.**

- A **man** captures by jumping over a diagonally adjacent enemy piece onto the empty square directly beyond, **forward or backward**.
- A **king** captures an enemy piece at any distance along a diagonal provided every square between the king and the captured piece is empty, and lands on **any** empty square beyond it (there must be at least one).
- **Capturing is mandatory.** If the side to move has any capture available, a non-capturing move is rejected (`errors.move.captureMandatory`). When several captures are available the player chooses freely — there is no majority rule.
- **Chains.** After a capture, if the same piece can capture again it **must** continue; the player may not stop early or switch pieces (`errors.move.mustContinueCapture`). A king that has several landing squares after a capture must pick one from which a further capture is possible if any such square exists.
- **Turkish strike.** Captured pieces stay on the board until the chain ends and are then removed together. During a chain a pending-captured piece still blocks movement and **cannot be jumped a second time**; the capturing piece may cross the same empty square more than once.
- **Promotion.** A man that ends a step or a jump on the far rank becomes a king immediately. If it promoted mid-chain and can continue capturing **as a king**, it does so in the same turn (the Russian rule — unlike International draughts, where it must stop).

**End of the match.**

- **Win:** on a side's turn it has **no pieces left** or **no legal move** (all pieces blocked). The side that made the last move wins. There is no winning line — `MatchEnded` carries the final board, and the renderer crowns the winner ([`platform.md`](../platform.md) §3).
- **Draw by repetition:** the same position (board + side to move, evaluated at the end of a complete turn) occurs for the **third** time.
- **Draw by king shuffling:** **15 consecutive moves by each side** (30 plies) in which only kings moved and nothing was captured. Any man move or capture resets the count.
- The other codified Russian draw rules (three kings vs one on the long diagonal, specific endgame move limits) are **not** modelled in v1; a stuck endgame still resolves by the king-shuffling rule or on the clock. A draw offer is not available in v1 — it would need a platform-level handshake like rematch and is deferred ([`roadmap/open-questions.md`](../roadmap/open-questions.md)).

## Multi-capture chains on the wire

A **move is one step or one jump**, never a whole chain. Chains use the platform's turn-retention seam exactly like Sea Battle's hit-shoots-again: an accepted jump that leaves the same piece with a further capture returns `MoveResult.KeepTurn`, and the module records the obligation on the state (`chain`). The next move from that side must be a capture by that piece. Consequences, all intended:

- The opponent watches the chain unfold jump by jump; the last-move highlight follows the capturing piece.
- The mover's clock keeps running through the chain (they are choosing); the defender's never ticks.
- A reconnect snapshot mid-chain restores the obligation, because `chain` is part of the persisted state.
- Pending captures are removed when the chain ends; terminal detection and the draw counters are evaluated at that moment, never mid-chain.

## Wire vocabulary (module ↔ renderer agreement, opaque to the platform)

- **Coordinates:** `{ row: 0–7, col: 0–7 }`, `row 0` is black's home rank (top in white's orientation). The renderer flips the board for the black seat.
- **Move payload:** `{ from: { row, col }, to: { row, col } }`.
- **Module reject keys** (`CheckersErrors`, following the catalog-wide `errors.*` naming): `errors.move.notYourPiece`, `errors.move.illegalMove` (destination not reachable by that piece), `errors.move.captureMandatory`, `errors.move.mustContinueCapture`, plus the shared `errors.move.outOfBounds` and `errors.validation.move`.
- **State shape** (`Serialize`, also the Redis form):

  ```jsonc
  {
    "cells": [ /* 64 entries, row-major: null | "white" | "black" | "white-king" | "black-king" */ ],
    "sideToMove": "white",
    "moveCount": 0,                       // completed turns, not plies
    "lastMove": { "from": {…}, "to": {…}, "captured": {…} } | null,
    "chain": { "at": {…}, "captured": [ {…} ] } | null,   // obligation to keep capturing with the piece at `at`
    "legalMoves": [ { "from": {…}, "to": {…}, "captured": {…}? } ],  // for sideToMove, server-computed
    "whiteCount": 12, "blackCount": 12,
    "kingMovePlies": 0,                   // king-only, capture-free plies since the last reset
    "history": [ "…" ],                    // position keys since the last irreversible move (repetition rule)
    "drawReason": "repetition" | "kingMoves"   // present only on a drawn terminal state
  }
  ```

  `legalMoves` is derived and published so the renderer never re-implements the rules: it highlights movable pieces and target squares straight from the server's list, which also makes mandatory capture legible (only capturing pieces light up). `Deserialize` ignores it and recomputes. Everything else is module-owned; the platform routes it opaquely (CLAUDE.md §7 "Platform thinness").

## Renderer contract

Web feature at `apps/web/features/games/checkers/`, registered in the catalog `registry.ts`.

- **Board:** 8×8 with the dark squares playable; oriented so the caller's own pieces start at the bottom (black's seat sees the board flipped; spectators see white at the bottom). Mind the Safari `aspect-ratio` gotcha (don't double-declare on grid container + `1fr`-track cells).
- **Interaction:** tap a highlighted piece, then a highlighted destination; both highlights come from `legalMoves`. Tapping a second own piece re-selects. During a chain only the chaining piece is selectable. A rejected move surfaces the reject key inline per [`platform.md`](../platform.md) §2.2.
- **Pieces must be distinguishable without hue** (Connect 4's bar): white and black discs with contrasting borders, and kings carry a **crown glyph** rather than a colour change. Pending-captured pieces during a chain render faded.
- **Last-move highlight** ([`platform.md`](../platform.md) §3) = the opponent's latest `lastMove.to` square, plus a lighter mark on `from`.
- **Chain feedback:** a line under the board tells the mover they must keep capturing, and tells the opponent the chain is in progress (Sea Battle's hit-chain feedback precedent).
- **Counters:** live piece counts per side beside the board (as Reversi shows disc counts).
- **Terminal:** crown the winner; for draws show the reason (`drawReason`) in the result line.
- i18n: `games.checkers.*` keys (name, shortDescription, rules, side labels, board/cell a11y labels, chain and draw-reason strings) in **both** `packages/shared/src/i18n/ka.ts` and `en.ts`.
