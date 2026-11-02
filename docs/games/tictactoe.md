# Tic-Tac-Toe (`tictactoe`)

Canonical rules spec for the `tictactoe` module — see [`platform.md`](../platform.md) §2 for the catalog and the rules every module shares. The per-module README (`apps/api/src/PlayMe.Domain/Games/TicTacToe/RULES.md`) may expand on edge cases, but the canonical statement lives here.

| | |
|---|---|
| **Board** | N×N, `gameOptions: { boardSize ∈ {3, 6, 9} }` |
| **Sides** | **X** and **O** — X moves first |
| **Clock budget** | Scales with board size, per side: 3×3 → **1 min**, 6×6 → **3 min**, 9×9 → **5 min** ([`platform.md`](../platform.md) §1 #3) |

## Rules

N×N grid, players alternate placing X / O. Host picks `boardSize ∈ {3, 6, 9}` via `gameOptions` at room creation; win length derives deterministically:

- **3×3** — first to align **3 consecutive** marks wins.
- **6×6** — first to align **at least 4 consecutive** marks wins. A run of 5 or 6 in a row counts as a single win, not separately.
- **9×9** — first to align **at least 5 consecutive** marks wins (Gomoku-style first-to-5). Longer runs (6 / 7 / 8 / 9 in a row) also count as a single win. First-player advantage exists on 9×9 and is accepted for casual play; no swap / pro / balancing rule in v1.

Lines count horizontally, vertically, or along either diagonal. No wraparound. Board fills with no line → **draw**. **X moves first** regardless of board size.

## Bot

The module implements `IBotPlayer` (Sprint 12; [`bots.md`](../bots.md)). Every level first takes an immediate win and otherwise blocks an immediate loss; beyond that:

- **Easy** — a random legal cell near the existing marks.
- **Medium** — alpha-beta search to depth 2 over a window heuristic.
- **Hard** — 3×3: full search (solved game, the bot never loses). 6×6 and 9×9: alpha-beta to depth 3.

The heuristic sums, over every window of `winLength` cells, a weight growing geometrically with the number of own marks in an uncontested window, minus the same for the opponent. On 6×6 and 9×9 the search considers only empty cells within two steps of an existing mark (the centre on an empty board). Search depth and the candidate filter are fixed, never a timer, so a given board, side, level and seed always produce the same move; the rng breaks ties at the root only.

Measured think time (Sprint 12 PR5, Apple-silicon laptop, release build, after warm-up): Hard on a scattered mid-game 9×9 board ≈ 125 ms per move, Medium ≈ 30 ms; 6×6 under 1 ms at both. Comfortably inside the 1000 ms production target of [`bots.md`](../bots.md) §2.2 even at several times slower.
