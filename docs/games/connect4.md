# Connect 4 (`connect4`)

Canonical rules spec for the `connect4` module — see [`platform.md`](../platform.md) §2 for the catalog and the rules every module shares. The per-module README (`apps/api/src/PlayMe.Domain/Games/Connect4/RULES.md`) may expand on edge cases, but the canonical statement lives here.

| | |
|---|---|
| **Board** | 7 columns × 6 rows, gravity |
| **Sides** | **red** and **yellow** (traditional pair) — red moves first |
| **Default clock budget** | 3 min per side ([`platform.md`](../platform.md) §1 #3) |
| **Game options** | none |

## Rules

7-column × 6-row board with **gravity**: a dropped disc occupies the lowest empty cell of the chosen column. Players alternate dropping **red** and **yellow** discs. First to align **4 consecutive** discs (horizontal, vertical, or either diagonal) wins. A column with no empty cells is not a legal target. Whole board fills with no line → **draw**. **Red moves first** by Hasbro convention; the host's color choice at room creation therefore implicitly decides who starts (platform rule [`platform.md`](../platform.md) §1 #11).

## Piece rendering (accessibility)

Red and yellow are perceptually close for the most common forms of color-blindness (deuteranopia / protanopia, ~5% of male players), so the two sides must be distinguishable without relying on hue alone. Render **red as a solid disc** and **yellow as a ring (donut)** — same outer circle, yellow has a transparent inner hole. This preserves Connect 4's "stacked discs" visual identity, keeps both sides symmetric in shape, and remains legible in monochrome, high-contrast mode, screenshots, and at small mobile sizes. The win-line highlight should glow around both shapes equally. Do **not** distinguish the two players by changing the outer shape (e.g. circle vs. triangle) — that breaks the gravity/stacking intuition that defines Connect 4.
