# Sprint 8 — Reversi (~1–2 weeks)

First post-MVP game. Canonical rules in [`games/reversi.md`](../games/reversi.md).

- New self-contained game module `reversi` (8×8, classic free central-square opening, dark/light discs, auto-pass via renderer-emitted + server-validated synthetic move, draw on tie). `DefaultClockBudget` = 10:00 per side.
- Reuses the platform layer entirely. **If you need to modify the platform to add it, that's a design bug — fix the seam first.**
- DI registration (one line each in `AddDomain.cs` + `AddApplication.cs`). Web renderer at `apps/web/features/games/reversi/` (opening-phase visual cue on the central 2×2, last-move highlight, live disc counters, auto-pass toast). `games.reversi.*` keys in both `packages/shared/src/i18n/ka.ts` and `en.ts`. Catalog entry registered; landing grid grows from four cards to five.

**Exit criteria:** Reversi plays correctly end-to-end with clock, reconnect, rematch (with side-swap), and resign. Auto-pass, double-auto-pass terminal, and tie outcome all verified. No platform code was modified (only added) — `git diff main -- 'apps/api/src/PlayMe.Domain/Platform/' 'apps/api/src/PlayMe.Api/Hubs/' 'apps/api/src/PlayMe.Infrastructure/Redis/'` is empty. **Sprint 8 closed 2026-05-22.**

Auto-pass design note: the original wording read "the server flips the turn automatically without expecting a move from the client" — that wasn't reachable because `SubmitMoveHandler` always sets `nextSide = OtherSide(callerSide)` and `MoveResult` has no override field. Implemented as: the module publishes a `mustPassSide` flag on the per-game state when the side-to-move has no legal placement; the Reversi renderer reads it and submits a synthetic `{ pass: true }` move (no UI, no user action) which the server re-validates against the board. Same seam will work for any future non-strict-alternation game (Mancala same-side-again, Go-style pass) without a platform extension.
