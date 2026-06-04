# Sprint 9 — Per-game options + unified Tic-Tac-Toe (~1–2 weeks)

First post-launch refactor. Introduces the **per-game options seam** — an immutable `gameOptions` payload on the room aggregate, opaque to the platform layer and validated by the game module — and uses it to collapse the three TTT modules (`tictactoe-3x3` / `-6x6` / `-9x9`) into one `tictactoe` module with a configurable `boardSize ∈ {3, 6, 9}` driving `winLength ∈ {3, 4, 5}`. The catalog goes from five tiles to three (Tic-Tac-Toe, Connect 4, Reversi). After this sprint, future games can expose their own configure-page knobs without further platform changes; this is one of the explicit "platform scope grows" moments allowed by §7 platform-thinness.

Lands as three PRs, in order, each squash-merged to `main`:

- **PR1 — additive: `gameOptions` seam + new `tictactoe` module.** New `tictactoe` module alongside the existing three. `Room` aggregate carries `gameOptions` (opaque JSON); `IGameModule` gains `ValidateOptions(json)` and `Initialize(options)`; `CreateRoomCommand` / `CreateRoomHandler` accept and persist them; `JoinInfo` / `RoomDto` expose them so the challenger sees "Tic-Tac-Toe · 6×6 · 3 min" in onboarding before they commit. Per-game time-limit defaults move from the platform doc table into the module (`boardSize: 3 → 3 min`, `6 → 3 min`, `9 → 10 min`). All three TTT presets remain selectable. The old three modules stay registered — nothing breaks yet.
- **PR2 — cutover: catalog, UI, SEO point at the new module.** Catalog grid shows one "Tic-Tac-Toe" tile; configure page exposes a board-size segmented control next to the existing time-limit and side controls. New i18n keys `games.tictactoe.{name,shortDescription,rules}` (rules text varies by `boardSize` — three rule variants under one key namespace, or three sibling keys, decide in PR). `/games/tictactoe-{3x3,6x6,9x9}` slugs **301 to `/games/tictactoe?size={3,6,9}`** for a 2-week redirect window so invite links sitting in chats keep resolving. Sitemap regenerated; old slugs dropped from `sitemap.ts` (the 301 keeps them addressable, but they shouldn't be advertised as canonical). Old `games.tictactoe-{3x3,6x6,9x9}.*` i18n keys remain in this PR.
- **PR3 — delete the old three modules.** Lands no sooner than 2 weeks after PR2 to honour the redirect window. Removes domain projects (`PlayMe.Domain/Games/TicTacToe{3x3,6x6,9x9}/`), the matching `apps/web/features/games/tictactoe-{3x3,6x6,9x9}/` folders, DI registrations, the redirect rules, the old i18n keys, and the old catalog entries.

Cross-cutting doc touches (land inside the sprint PRs, not after):
- [`platform.md`](../platform.md) §2 catalog table: collapse the three TTT rows. §1 #3 time-limit defaults: note that per-game defaults live in the module now, not the doc.
- [`CLAUDE.md`](../../CLAUDE.md) §1 catalog mention: "Tic-Tac-Toe 3×3 / 6×6 / 9×9" → "Tic-Tac-Toe (configurable board size: 3×3 / 6×6 / 9×9)".
- [`open-questions.md`](open-questions.md): revise the "New games are net-new modules, not parameterizations of existing ones" line — net-new modules remain the default for genuinely different games, but per-game configurable knobs (board size, variant toggles) now go through `gameOptions` on the existing module rather than spawning siblings. Also update "fixed at four modules for MVP" — stale since Sprint 8.

Invariants the sprint must preserve:
- `gameOptions` is **immutable for the room's lifetime**, including rematches in the same room. Side-swap on rematch only flips sides; options never change. Reconnect returns the original options unchanged.
- Platform code (`Domain/Platform/`, `Api/Hubs/`, `Infrastructure/Redis/`) sees `gameOptions` only as **opaque bytes** — no `switch` on `boardSize` outside the module. Same diff test as Sprint 8: after PR3, the platform paths should show only the additive plumbing for `gameOptions` (one new field on `Room`, one new param on `CreateRoom`, one new field in `RoomDto`/`JoinInfo`), nothing game-specific.
- Validation lives in the module via `ValidateOptions`. Invalid payloads reject at the API boundary with the existing FluentValidation / `ProblemDetails` pattern and a localized error key (`errors.config.invalidGameOptions` or similar).
- Server is still the only authority. The configure page's board-size picker is a *request*; the server re-validates options against the module before committing room state.

**Exit criteria:** Landing grid shows three tiles. Configure page picks board size for TTT; the challenger sees the chosen size in onboarding before joining. All three sizes play correctly end-to-end with clock, reconnect, rematch (with side swap), and resign. Old slug URLs 301 to the new one. No platform code path branches on `boardSize` or any other game-specific field.

**Status:** Shipped 2026-05-23.

| # | PR | What |
|---:|---|---|
| 96 | docs | Sprint 9 plan |
| 97 | PR1a | `gameOptions` seam — `IGameModule.ValidateOptions`/`NewMatch(options)`, `Room.GameOptions`, `RoomRecord`/`RoomDto` plumbing, surface validation (1 KiB raw-JSON cap), 12 new seam tests |
| 98 | PR1b | Unified `tictactoe` module + parser + 65 new tests (`boardSize ∈ {3,6,9}`, win length derived 3/4/5, directional-sweep win detection) |
| 99 | PR2 | Catalog/UI cutover — one tile, board-size segmented control, `?size=` pre-select from 308 redirects, new `games.tictactoe.{name,shortDescription,rules}` i18n keys |
| 100 | PR3 | Drop legacy `tictactoe-{3x3,6x6,9x9}` modules + parsers + renderers + redirects + deprecated i18n keys (54 files, +86 / −2549) |

Variances from the plan worth recording:

- **PR1 split into PR1a + PR1b for reviewability.** The plan called for a single PR; each half landed as its own merge so the platform seam and its first consumer could be reviewed independently. Mirrored the Sprint 8 Reversi api/web split precedent.
- **PR3 landed immediately, not after a 2-week redirect window.** The original window protected invite-link tails sitting in chats; the site is still pre-launch with only the developer using it, so there was no traffic to drain. Server-side rooms with legacy gameIds drain in 30 minutes per `RoomLifetimes.WaitingForOpponent`.
- **Size-driven clock defaults dropped from scope.** The plan called for `boardSize: 3 → 3 min, 6 → 3 min, 9 → 10 min` defaults on the configure page; there's no host-side clock picker today, so the module's single `DefaultClockBudget = 3 min` is the only knob. Listed in [`deferred-polish.md`](deferred-polish.md).
