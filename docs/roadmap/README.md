# Implementation roadmap

The recommended construction sequence. Each sprint should land an **end-to-end vertical slice** (deployable, demonstrable), not a horizontal layer. Sprint lengths are nominal — slip-or-cut scope, never quality. The first vertical slice is the highest-risk piece because it exercises every layer; don't add features until it ships.

One file per sprint; a sprint's file is the plan before it ships and the record (status, PR table, variances) after. Open questions deferred to v2 live in [`open-questions.md`](open-questions.md); smaller postponed-but-in-scope items in [`deferred-polish.md`](deferred-polish.md).

## Sprints

| # | Slice | Status |
|---:|---|---|
| [0](sprint-00-bootstrap.md) | Bootstrap — hello-world through every piece of infrastructure | Shipped |
| [1](sprint-01-tictactoe-3x3.md) | Tic-Tac-Toe 3×3, end-to-end | Shipped |
| [2](sprint-02-clock-reconnect.md) | Chess clock + reconnect | Shipped |
| [3](sprint-03-connect4.md) | Connect 4 | Shipped |
| [4](sprint-04-tictactoe-6x6-9x9.md) | Tic-Tac-Toe 6×6 and 9×9 | Shipped |
| [5](sprint-05-rematch-resign.md) | Rematch + resign | Shipped |
| [6](sprint-06-i18n-seo-pwa-theming.md) | i18n + SEO + PWA + theming | Shipped 2026-05-20 |
| [7](sprint-07-hardening.md) | Hardening for launch | Closed 2026-05-20 — public launch |
| [8](sprint-08-reversi.md) | Reversi | Closed 2026-05-22 |
| [9](sprint-09-game-options.md) | Per-game options + unified Tic-Tac-Toe | Shipped 2026-05-23 |
| [10](sprint-10-sea-battle.md) | Sea Battle — first hidden-information game + three platform seams | Shipped 2026-06-04 |

## Roadmap rules

- **The first end-to-end slice (Sprint 1) is the canary** for whether the platform layer is right. Don't add anything to a later sprint until Sprint 1 ships.
- **No game-module work before Sprint 1.** Stub everything; defer until the platform skeleton is real.
- **Adding a new game (Sprint 3 onward) must be a pure addition.** If you modify the platform to add a game, fix the seam, then continue.
- **A sprint always lands a deployable, demonstrable slice.** Never split a sprint into "build, then make it work."
- **Sprint 7 is non-negotiable before public launch.** Going live without rate limits, CSP, or error monitoring is how products eat dirt.
