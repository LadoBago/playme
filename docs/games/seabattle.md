# Sea Battle (`seabattle`)

> **Status: planned — not yet implemented.** Sprint plan: [`roadmap/sprint-10-sea-battle.md`](../roadmap/sprint-10-sea-battle.md). This file is the canonical rules spec the implementation must match; see [`platform.md`](../platform.md) §2 for the catalog and the rules every module shares.

First **hidden-information** game in the catalog. Post-Soviet ruleset (Морской бой as played on grid paper), **not** the Hasbro variant. Display names: ka **„ჩაძირობანა"**, en **"Sea Battle"**.

| | |
|---|---|
| **Board** | 10×10 grid per player (two grids per match) |
| **Sides** | **first** and **second** — `first` shoots first; the side label carries no other asymmetry |
| **Default clock budget** | 10 min per side ([`platform.md`](../platform.md) §1 #3) |
| **Setup budget** | 2 min, module-declared (`SetupBudget`) — see Setup phase |
| **Game options** | none in v1 (`ValidateOptions` requires `null`; future variants — salvo mode, alternative fleets — go through `gameOptions` per the Sprint 9 pattern) |
| **Platform seams used** | `IGameModule` + `IHiddenStateGame` (hidden state) + `ISetupGame` (setup phase) + `MoveResult.KeepTurn` (extra shots) |

## Rules

- Each player has a hidden **fleet of 10 ships, 20 cells**: 1× four-decker, 2× three-decker, 3× two-decker, 4× single-decker.
- Ships are straight horizontal or vertical lines of cells. **Ships may not touch — not even diagonally:** every cell in the 8-neighborhood of a ship cell must be water.
- Players shoot in turns at cells of the opponent's grid. The server answers **miss**, **hit**, or **sunk** (the ship's last remaining cell was hit; a sunk announcement implicitly reveals the whole ship).
- **A hit or sunk earns another shot** — the shooter keeps the turn until a miss (`MoveResult.KeepTurn`).
- A shot at an already-shot cell is **rejected** (`seabattle.alreadyShot`); per [`platform.md`](../platform.md) §2.2 the clock keeps running and the renderer surfaces an inline error.
- A shot at a never-shot cell that deduction proves empty (a neighbor of a sunk ship) is **legal** — a wasted but valid miss. The renderer dims those cells as a QoL hint; the server doesn't care.
- **Win:** all 20 of the opponent's ship cells hit. **A draw is impossible** — the player completing the 20th hit wins immediately.
- There is no winning line. Like Reversi, `MatchEnded` instead carries the **full revealed final state** (both fleets), so the loser sees where the remaining ships were ([`platform.md`](../platform.md) §3).

## Setup phase (fleet placement)

v1 ships **random placement with reroll**; manual drag-and-drop placement is deferred ([`roadmap/deferred-polish.md`](../roadmap/deferred-polish.md)).

- On entering `SettingUp`, each player's client generates a uniformly random **legal** fleet locally using `crypto.getRandomValues` (placement must be unpredictable to the opponent — an adversarial-fairness concern, so the cryptographic-RNG rule applies, not `Math.random()`).
- **Reroll is client-local** — regenerate and re-render instantly, zero server round-trips.
- **Commit** sends the fleet to the server exactly once (`SubmitSetup`); the module validates it exhaustively (ship count and shapes, bounds, the no-touch rule) and stores it server-side. It is never broadcast. A second commit is rejected (`seabattle.alreadyCommitted`) — reroll is client-local, so replace semantics aren't needed.
- The opponent sees only a role-level readiness signal (`OpponentSetupCommitted`), never the fleet.
- Both committed → `InProgress`, `MatchStarted`, clock starts. The **setup phase is unclocked**; the module-declared 2-min setup budget and in-match-style presence tracking backstop a player who stalls or leaves (see the sprint plan, seam C).
- Rematches re-enter `SettingUp` with **fresh fleets** every match. The side swap ([`platform.md`](../platform.md) §1 #15) alternates the first-shot advantage across the series.

## Hidden information (per-viewer projection)

The module implements `IHiddenStateGame.SerializeFor(state, viewerSide)`. While the match has no `Outcome`, each player's wire state contains:

- **own grid** — full fleet plus the opponent's incoming shots;
- **opponent grid** — only the viewer's outgoing shots and their results (miss / hit / sunk, with sunk ships fully revealed by definition);
- shared scalars — phase, whose turn, both players' readiness during setup.

The opponent's un-hit fleet **never crosses the wire pre-terminal** — projection happens server-side at the wire boundary; Redis always stores the full state. Once the match has any `Outcome` (including resign / timeout / disconnect), both players receive the full unprojected state.

## Wire vocabulary (module ↔ renderer agreement, opaque to the platform)

- **Move payload:** `{ x: 0–9, y: 0–9 }` — a shot at the opponent's grid.
- **Setup payload:** `{ ships: [{ x, y, length, horizontal }, …] }` — exactly 10 entries; `(x, y)` is the ship's top-left anchor.
- **Module reject keys** (`SeaBattleErrors`, following the catalog-wide `errors.*` naming so the shared i18n catalog serves them): `errors.move.alreadyShot`, `errors.setup.invalidFleet` (one key for every composition violation — legal clients generate fleets locally, so an invalid commit is a bug or tampering), plus the shared `errors.move.outOfBounds` and `errors.validation.move`. The platform owns `errors.setup.notInSetup` and `errors.setup.alreadyCommitted` (seam C rejects double commits before the module is consulted).
- **Live projection shape** (`SerializeFor`): `{ phase: "setup"|"battle", viewerSide?, yourFleet?, shots: { first: [{x,y,result}], second: […] }, sunk: { first: [ships sunk BY first], second: […] } }` — public knowledge (shot results, sunk ships) for everyone, plus the viewer's own fleet; `result ∈ miss|hit|sunk`, with earlier hits on a finished ship retroactively reading `sunk`. The terminal reveal ships the full persisted shape instead: `{ firstFleet, secondFleet, shotsByFirst, shotsBySecond }` — the renderer handles both.

State shape, result encoding, and these keys are module-owned; the platform routes them opaquely (CLAUDE.md §7 "Platform thinness").

## Renderer contract

Web feature at `apps/web/features/games/seabattle/`, registered in the catalog `registry.ts`.

- **Setup screen:** fleet preview on own grid, **Reroll** and **Commit** buttons, then a "waiting for opponent" state showing the opponent's readiness.
- **Battle screen: two grids** — own fleet grid (shows incoming shots) and targeting grid (own shots + results). Mobile: grids stack vertically; mind the Safari `aspect-ratio` gotcha (don't double-declare on grid container + `1fr`-track cells).
- **Hit / miss / sunk must be distinguishable without hue** (same accessibility bar as Connect 4's disc/ring rule): dot for miss, ✕ for hit, sunk ships outlined as whole ships.
- **Last-move highlight** ([`platform.md`](../platform.md) §3) = the opponent's latest shot on the own-fleet grid. During an opponent hit-chain, the highlight moves shot to shot.
- Cells deduction proves empty (neighbors of sunk ships) render dimmed on the targeting grid — client QoL only; they remain legal targets.
- On `MatchEnded`, render the revealed boards (the loser sees the surviving fleet) and crown the winner — no winning-line treatment.
- i18n: `games.seabattle.*` keys (name, shortDescription, rules, setup strings, hit/miss/sunk announcements) in **both** `packages/shared/src/i18n/ka.ts` and `en.ts`.
