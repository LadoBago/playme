# Sprint 11 — Checkers (~1–2 weeks)

Fifth catalog game. Canonical rules in [`games/checkers.md`](../games/checkers.md) — **Russian draughts**, the ruleset used across Georgia, matching the post-Soviet convention Sea Battle set in Sprint 10.

## Why this game, why these rules

- Checkers is the most-requested classic missing from the catalog and the first game with **piece movement** (every earlier module places or shoots at cells). It exercises the move pipeline with a two-coordinate payload and multi-step turns, both of which the platform already supports opaquely.
- Russian draughts over Anglo-American: it is what Georgian players know, and the rule differences (backward man captures, flying kings, promotion mid-chain) are all module-internal. An Anglo-American variant later is a `gameOptions` knob, not a new module ([`open-questions.md`](open-questions.md) "More games").

## Platform fit — zero platform diff expected

Every rule maps onto an existing seam; no new capability is needed:

| Rule | Mechanism |
|---|---|
| Multi-capture chains | `MoveResult.KeepTurn` after each jump that leaves a further capture; the obligation lives on the module state (`chain`). Same seam as Sea Battle's hit-shoots-again and Reversi's forced skip. |
| Mandatory capture, chain continuation | Module validation → `MoveResult.Reject(CheckersErrors.*)`; the clock keeps running per [`platform.md`](../platform.md) §2.2. |
| Win by no pieces / no legal move | Module-evaluated after each turn-ending move → `Outcome.Win(side)`, no winning line (Reversi precedent). |
| Draw by repetition / king shuffling | Module-tracked counters on the state → `Outcome.Draw`; `drawReason` is a module rendering hint. |
| Board orientation per seat | Renderer-only (`callerSide`). |

**If adding Checkers forces an edit in `Domain/Platform/`, the platform handlers, the Hub, or `Infrastructure/Redis/`, the seam is wrong — fix it first** (CLAUDE.md §7).

## Design decisions recorded up front

- **One move = one step or one jump.** A whole-chain payload (`path: […]`) was considered and rejected: it needs a path validator with its own vocabulary, hides the chain from the opponent until it completes, and duplicates what `KeepTurn` already provides. The per-jump design costs one extra round-trip per capture in a chain — negligible against a 10-minute clock.
- **Legal moves are published by the server** in the serialized state. The renderer highlights from that list and never re-implements the rules, honouring the "server is the single source of truth / no client game logic that could disagree" rule. Perfect-information game, so nothing leaks.
- **Draw rules limited to repetition + 15 king-moves-each.** The remaining codified Russian endgame limits are out of scope for v1; a draw *offer* needs a platform handshake and is deferred.
- **Sides are `white` / `black`**, standard draughts vocabulary, distinct from Reversi's `dark` / `light`. Side identifiers are module vocabulary; the renderer uses theme tokens for actual colours.

## The game module + web (pure addition)

- `Domain/Games/Checkers/` — state (64 cells, side to move, chain obligation, draw counters, position history), `CheckersMove` (`from`/`to`), `CheckersSides`, `CheckersErrors`, `CheckersCoordinate` (per-game copy, by design), module implementing `IGameModule`. `Application/Games/Checkers/CheckersMoveParser.cs` for the `{ from: {row,col}, to: {row,col} }` payload. DI registration (one line each in `AddDomain.cs` / `AddApplication.cs`).
- Web renderer at `apps/web/features/games/checkers/` per the renderer contract in [`games/checkers.md`](../games/checkers.md); catalog entry in `packages/shared/src/catalog/games.ts` — landing grid grows to **five** tiles; landing-card preview token branch for the checkers pieces.
- i18n: `games.checkers.*` plus the new `errors.move.{notYourPiece,illegalMove,captureMandatory,mustContinueCapture}` in **both** `ka.ts` and `en.ts`. ka name **„შაში"**; king **„დამკა"** (colloquial, to be confirmed with the native speaker — alternative „მეფე").
- `/play/checkers` gets the full SEO surface automatically from the shared `[game]` route (metadata, canonical, hreflang, JSON-LD, sitemap entry) — verify with Lighthouse in both locales and themes before closing.

## PR breakdown (each squash-merged, in order)

| PR | What | Platform diff allowed? |
|---|---|---|
| PR1 | docs: this plan + [`games/checkers.md`](../games/checkers.md) + roadmap index row | — |
| PR2 | `checkers` domain module + parser + module/parser tests (no catalog entry yet — unreachable from the web) | **none** |
| PR3 | web renderer + shared catalog entry + i18n + landing preview + `CLAUDE.md` §1 catalog line + `platform.md` §2 table / §4 glossary + `frontend.md` lists + `README.md` + [`open-questions.md`](open-questions.md) "More games" count | **none** |

Cross-cutting doc touches land inside the listed PRs, not after ([`sprint-10-sea-battle.md`](sprint-10-sea-battle.md) precedent).

## Exit criteria

- Checkers plays end-to-end with clock, reconnect (including mid-chain), resign, rematch (side swap alternates the first move), and the series scoreboard.
- Rules verified by module tests: man step/capture both directions, flying-king move and capture, mandatory capture, chain continuation and the king's must-continue landing rule, Turkish strike (no double jump, pending pieces block), promotion mid-chain continuing as a king, win by no pieces and by blockade, draw by repetition and by king shuffling, serialize/deserialize round trip incl. an in-progress chain.
- Renderer: both seats see their own pieces at the bottom; highlights come only from `legalMoves`; pieces and kings distinguishable without hue; verified in both themes, Chrome and Safari.
- Existing four games show **zero behavioral change**; `git diff main -- 'apps/api/src/PlayMe.Domain/Platform/' 'apps/api/src/PlayMe.Api/Hubs/' 'apps/api/src/PlayMe.Infrastructure/Redis/'` is empty at PR2 and PR3.
- Lighthouse green on `/play/checkers` in both locales and both themes.

## Status

**Shipped 2026-10-31.** Three PRs, squash-merged in order:

| # | PR | What |
|---:|---|---|
| 202 | docs | This plan + [`games/checkers.md`](../games/checkers.md) canonical spec (re-opened from #200, which had been based on the wrong branch) |
| 203 | module | `checkers` domain module + `CheckersMoveParser` + 36 tests; zero platform diff |
| PR3 | web | Renderer, shared catalog entry, i18n (ka/en), landing preview, cross-cutting doc touches |

Deviation from the plan: the chain feedback ("keep capturing" / "opponent is mid-capture") sits beside the platform turn pill via `GameModule.TurnStatusExtra`, the Sea Battle precedent, rather than on a line under the board. Georgian king term shipped as „დამკა" pending native confirmation.
