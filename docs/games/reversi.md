# Reversi (`reversi`)

Canonical rules spec for the `reversi` module — see [`platform.md`](../platform.md) §2 for the catalog and the rules every module shares. The per-module README (`apps/api/src/PlayMe.Domain/Games/Reversi/RULES.md`) may expand on edge cases, but the canonical statement lives here.

| | |
|---|---|
| **Board** | 8×8 |
| **Sides** | **dark** and **light** — dark moves first |
| **Default clock budget** | 10 min per side ([`platform.md`](../platform.md) §1 #3) |
| **Game options** | none |

## Rules

8×8 grid, two sides **dark** and **light** placing two-sided discs. **Classic free opening:** the first four placements are restricted to the central 2×2 squares (d4/d5/e4/e5). Players alternate (dark, light, dark, light); no flipping occurs during the opening because no brackets can form on an otherwise-empty board. **Standard placement (move 5+):** a legal move must bracket ≥1 contiguous opponent disc in some straight line (horizontal, vertical, or diagonal) between the placed disc and another disc of the mover's color. **All** bracketed opponent discs in **every** direction flip in a single move. A placement that flips nothing is illegal. **Auto-pass:** when a side has no legal placement, that side passes — without any user-facing action. The server publishes a `mustPassSide` flag on the per-game state when the side-to-move is forced to pass; the renderer reads the flag and submits a synthetic pass move on the player's behalf. The pass is a regular module-owned `GameMove` shape (`{ pass: true }` on the wire); the server re-validates that the side genuinely has no legal placements and rejects the pass otherwise. The platform never sees pass vocabulary — it routes the move opaquely (CLAUDE.md §7 "Platform thinness"). **End:** the game ends when the board is full or both sides pass consecutively. Higher disc count wins; equal counts → **draw**. **Dark moves first.**

There is no winning line — on match end the server emits the final disc counts (`{ dark, light }`) in `MatchEnded`, and the renderer crowns the higher-count side ([`platform.md`](../platform.md) §3).
