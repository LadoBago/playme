# Implementation roadmap and open questions

## 1. Implementation roadmap

The recommended construction sequence. Each sprint should land an **end-to-end vertical slice** (deployable, demonstrable), not a horizontal layer. Sprint lengths are nominal — slip-or-cut scope, never quality. The first vertical slice is the highest-risk piece because it exercises every layer; don't add features until it ships.

**Sprint 0 — Bootstrap (~1 week).** A hello-world that exercises every piece of infrastructure.

- Initialize the monorepo (pnpm + Turborepo) per [`architecture.md`](architecture.md) §2.
- Scaffold `apps/api` with the four Clean Architecture projects (`PlayMe.Domain`, `PlayMe.Application`, `PlayMe.Infrastructure`, `PlayMe.Api`); project references enforce the dependency rule.
- Scaffold `apps/web` (Next.js App Router, TS strict) with a placeholder landing page.
- Scaffold `packages/shared` (TS-only) and `packages/config` (base eslint/prettier/tsconfig).
- `infra/docker-compose.yml` for local Redis. Wire the Redis backplane in SignalR.
- `.editorconfig`, GitHub Actions CI (lint + typecheck + `dotnet build` + `dotnet format --verify-no-changes`).
- Deploy: API to Azure App Service for Linux, web to Vercel.
- Wire Sentry on both ends, PostHog on the web (no events yet).
- Trivial `GET /api/health` endpoint called by the landing page.

**Exit criteria:** Both ends boot, deploys are green, web reaches the API over WSS, Sentry sees a deliberate test error.

**Sprint 1 — Tic-Tac-Toe 3×3, end-to-end (~1–2 weeks).** First real vertical slice. **No clock, no rematch, no reconnect.** Create-room → join → play → win/draw.

- Domain: `Room`, `Match`, `Move`, `Outcome`, the `tictactoe-3x3` module with full rules.
- Application: `CreateRoomHandler`, `JoinRoomHandler`, `SubmitMoveHandler`, FluentValidation validators, ports (`IRoomRepository`, `IRoomCodeGenerator`).
- Infrastructure: `RedisRoomRepository` using the **Redis distributed-lock pattern from [`state.md`](state.md) §1** (`LockTakeAsync` / `LockReleaseAsync` on `playme:room:{code}:lock`, 5 s TTL, ~500 ms acquire budget). `RoomCodeGenerator` (cryptographic RNG per [`security.md`](security.md) §4).
- API: `RoomsController` (POST `/api/rooms`, GET `/api/rooms/{code}`), `RoomHub` at `/hubs/room` (per [`architecture.md`](architecture.md) §3) with `JoinRoom` and `SubmitMove` methods.
- Web: landing card grid (one card) **with the "How PlayMe works" section already included**, configure page **with a rules tab/panel**, room/match page with the board UI (including the **last-move highlight**) and a "share link" button.
- Generated API client wired (`pnpm gen:api`).
- Server-authoritative validation + win detection. `MatchEnded` includes the winning-line coordinates.

**Exit criteria:** Two browser tabs play a full game from a shared link; illegal moves are rejected with a clear error; the server is the rules authority.

**Sprint 2 — Chess clock + reconnect (~1 week).**

- `IClock` in `Application/Abstractions`; `SystemClock` in `Infrastructure`.
- Server-side **lazy** clock (no background per-room timer). Each state-mutating event (`MatchStarted`, `MoveAccepted`, `MatchEnded`, `OpponentDisconnected`/`Reconnected`, presence responses) carries a `ClockSnapshotDto`. The `ClockTick` event name is **reserved but not emitted** — the snapshot rides on existing events and the client interpolates locally. See [`state.md`](state.md) §2.2.
- Match ends on timeout → `MatchEnded` with `Outcome.Timeout`.
- Client renders the countdown by extrapolating the last snapshot locally — no client-side free-run, no client-authoritative timing.
- SignalR reconnect with a 30s grace window. Clock keeps running through disconnect. New events: `OpponentDisconnected`, `OpponentReconnected`.

**Exit criteria:** A game can time out; a player can close and reopen a tab within 30s without losing state.

**Sprint 3 — Connect 4 (~1 week).**

- New self-contained game module `connect4` (gravity, red/yellow discs, the disc-vs-ring rendering from [`games/connect4.md`](games/connect4.md)).
- Reuses the platform layer entirely. **If you need to modify the platform to add it, that's a design bug — fix the seam first.**
- Landing grid grows to two cards.

**Exit criteria:** Connect 4 plays correctly end-to-end with clock and reconnect; no platform code was modified (only added).

**Sprint 4 — Tic-Tac-Toe 6×6 and 9×9 (~1 week).**

- Two more independent game modules. The work should feel mechanical.
- Catalog grid grows to four cards.

**Exit criteria:** All four MVP games are playable. Adding a game is a module choice, not a platform one.

**Sprint 5 — Rematch + resign (~1 week).**

- Rematch handshake: `OfferRematch`, `AcceptRematch`, `RejectRematch`. The asymmetric exit from [`platform.md`](platform.md) §1 #10 (rejector auto-redirects; offerer stays with a notice and a manual exit button).
- Resign with the confirmation step ([`platform.md`](platform.md) §1 #8).
- `Ended` and `AwaitingRematch` states wired per [`state.md`](state.md) §2.
- **Series scoreboard** ([`platform.md`](platform.md) §1 #13): server-side counter in the room state (`{ host, challenger, draws }`), updated on every `MatchEnded`, displayed in the in-match UI for both players. Reset only when the room reaches `Closed`/`Expired`.
- **Side swap on rematch** ([`platform.md`](platform.md) §1 #15): on every accepted rematch, the server swaps `hostSide` and `challengerSide` before emitting `MatchStarted`. UI shows each player's current side in the HUD so the swap is obvious to both players.

**Exit criteria:** All four games can be played, resigned, finished, rematched (accepted/rejected), and exited cleanly.

**Sprint 6 — i18n + SEO + PWA + theming (~1–2 weeks).**

- i18next + `ka.json` and `en.json`. Every visible string moves behind a key.
- SEO: Next.js metadata, canonical, hreflang, sitemap, robots, JSON-LD on landing and per-game pages.
- PWA: manifest, icons, install prompt, service worker for offline shell.
- Theming: `next-themes`, semantic tokens in `globals.css`, light/dark/system, FOUC-prevention script.
- Accessibility pass: WCAG AA contrast in both themes, Connect 4 disc/ring legibility, focus rings, keyboard navigation.

**Exit criteria:** Lighthouse green (perf, a11y, SEO, best practices) on landing in both locales and both themes.

**Status:** Shipped 2026-05-20. Lighthouse desktop on `next start`:

| URL | Theme | Perf | A11y | Best Practices | SEO |
|---|---|---:|---:|---:|---:|
| `/` (ka) | light | 98 | 100 | 92 | 100 |
| `/` (ka) | dark | 100 | 100 | 92 | 100 |
| `/en` | light | 100 | 100 | 96 | 100 |
| `/en` | dark | 100 | 100 | 96 | 100 |

All four categories ≥ 90 across both locales and both themes. The Best Practices delta on the ka pages (92 vs 96) traces to `errors-in-console` / `inspector-issues` audits — not investigated; if anyone wants to push everything to 100, that's the lead.

**Sprint 7 — Hardening for launch (~1 week).**

Shipped (as of 2026-05-20):

- Rate-limit policies on hot endpoints ([`security.md`](security.md) §5) — `RoomsCreate` 10/min, `RoomsJoin` 5/min, `RoomsGet` 60/min, applied via `[EnableRateLimiting]` on the controllers; policies live in `apps/api/src/PlayMe.Api/RateLimiting/`.
- Security headers (CSP, HSTS, X-Frame-Options, etc.) — API `SecurityHeadersMiddleware` + Next.js `headers()` in `next.config.js` + per-request CSP nonce (`'nonce-<random>' 'strict-dynamic'`) in `apps/web/middleware.ts`.
- Localized error codes ([`observability-and-i18n.md`](observability-and-i18n.md) §2) — `errors.*` keys in `packages/shared/src/i18n/{ka,en}.ts`; friendly 404 / expired-room rendering via `apps/web/app/[locale]/not-found.tsx`.
- Production deploy with monitoring alerts wired to the on-call channel. Wired via `infra/provision.sh` (Azure CLI script) + `.github/workflows/deploy-api.yml` (GitHub Actions, OIDC → Azure, GHCR image). Alerts route to email; see [`security.md`](security.md) §11.
- PostHog instrumentation per [`observability-and-i18n.md`](observability-and-i18n.md) §1.2. Web-side: `room_created`, `room_joined`, `match_started`, `move_made` (in `configure-form.tsx`, `join-form.tsx`, `room-client.tsx`). Server-side: `match_ended` fires from all four match-end handlers (`SubmitMove`, `Resign`, `AdjudicateTimeout`, `AdjudicateDisconnectGrace`) through `IAnalyticsClient` (`Application/Abstractions/`) → `PostHogAnalyticsClient` (`Infrastructure/Telemetry/`, ~100 LOC `HttpClient` adapter, no SDK dep). `NoOpAnalyticsClient` is the DI fallback when `PostHog:ApiKey` is unset (dev/test). The `rematch_*` events stay on web (user actions, not authoritative outcomes).
- `room_expired` server-side analytics + `RoomExpired` SignalR event. New `playme:expires` sorted set + `RedisRoomExpirySweeperService` + `AdjudicateRoomExpiryHandler` mirror the timeout / disconnect-grace sweeper pattern. Scope: only `WaitingForOpponent` rooms (the funnel-meaningful "nobody joined" case); terminal-state cleanup-TTL expiries are GC, not tracked. Enrolled in `CreateRoomHandler` at `now + RoomLifetimes.WaitingForOpponent`; cancelled in `RegisterPresenceHandler` on the authoritative `WaitingForOpponent → InProgress` transition. The reserved `RoomExpired` SignalR event constant in `RoomHubEvents.cs` is now emitted; the web client renders a clean "this room has expired" terminal UI on receipt (`apps/web/app/[locale]/r/[code]/room-client.tsx`).
- Basic load test. `@playme/loadtest` workspace package in `infra/loadtest/` — TypeScript + `@microsoft/signalr`, runs end-to-end TicTacToe 3x3 games against a local API (host+challenger pair per room, random moves to a terminal `MatchEnded`). Paces room starts at `--ramp-per-min` (default 4/min) to respect the per-IP rate limits. First green capture (10 rooms, 4/min, 136s wall, 0 errors) recorded in [`loadtest.md`](loadtest.md) §7 along with the per-operation latency table and pass criteria for future runs.

**Exit criteria:** Public launch on playme.ge at the cost target from the deployment table in `CLAUDE.md` §4. **Sprint 7 closed 2026-05-20.**

**Sprint 8 — Reversi (~1–2 weeks).** First post-MVP game. Canonical rules in [`games/reversi.md`](games/reversi.md).

- New self-contained game module `reversi` (8×8, classic free central-square opening, dark/light discs, auto-pass via renderer-emitted + server-validated synthetic move, draw on tie). `DefaultClockBudget` = 10:00 per side.
- Reuses the platform layer entirely. **If you need to modify the platform to add it, that's a design bug — fix the seam first.**
- DI registration (one line each in `AddDomain.cs` + `AddApplication.cs`). Web renderer at `apps/web/features/games/reversi/` (opening-phase visual cue on the central 2×2, last-move highlight, live disc counters, auto-pass toast). `games.reversi.*` keys in both `packages/shared/src/i18n/ka.ts` and `en.ts`. Catalog entry registered; landing grid grows from four cards to five.

**Exit criteria:** Reversi plays correctly end-to-end with clock, reconnect, rematch (with side-swap), and resign. Auto-pass, double-auto-pass terminal, and tie outcome all verified. No platform code was modified (only added) — `git diff main -- 'apps/api/src/PlayMe.Domain/Platform/' 'apps/api/src/PlayMe.Api/Hubs/' 'apps/api/src/PlayMe.Infrastructure/Redis/'` is empty. **Sprint 8 closed 2026-05-22.**

Auto-pass design note: the original wording read "the server flips the turn automatically without expecting a move from the client" — that wasn't reachable because `SubmitMoveHandler` always sets `nextSide = OtherSide(callerSide)` and `MoveResult` has no override field. Implemented as: the module publishes a `mustPassSide` flag on the per-game state when the side-to-move has no legal placement; the Reversi renderer reads it and submits a synthetic `{ pass: true }` move (no UI, no user action) which the server re-validates against the board. Same seam will work for any future non-strict-alternation game (Mancala same-side-again, Go-style pass) without a platform extension.

**Sprint 9 — Per-game options + unified Tic-Tac-Toe (~1–2 weeks).** First post-launch refactor. Introduces the **per-game options seam** — an immutable `gameOptions` payload on the room aggregate, opaque to the platform layer and validated by the game module — and uses it to collapse the three TTT modules (`tictactoe-3x3` / `-6x6` / `-9x9`) into one `tictactoe` module with a configurable `boardSize ∈ {3, 6, 9}` driving `winLength ∈ {3, 4, 5}`. The catalog goes from five tiles to three (Tic-Tac-Toe, Connect 4, Reversi). After this sprint, future games can expose their own configure-page knobs without further platform changes; this is one of the explicit "platform scope grows" moments allowed by §7 platform-thinness.

Lands as three PRs, in order, each squash-merged to `main`:

- **PR1 — additive: `gameOptions` seam + new `tictactoe` module.** New `tictactoe` module alongside the existing three. `Room` aggregate carries `gameOptions` (opaque JSON); `IGameModule` gains `ValidateOptions(json)` and `Initialize(options)`; `CreateRoomCommand` / `CreateRoomHandler` accept and persist them; `JoinInfo` / `RoomDto` expose them so the challenger sees "Tic-Tac-Toe · 6×6 · 3 min" in onboarding before they commit. Per-game time-limit defaults move from the platform doc table into the module (`boardSize: 3 → 3 min`, `6 → 3 min`, `9 → 10 min`). All three TTT presets remain selectable. The old three modules stay registered — nothing breaks yet.
- **PR2 — cutover: catalog, UI, SEO point at the new module.** Catalog grid shows one "Tic-Tac-Toe" tile; configure page exposes a board-size segmented control next to the existing time-limit and side controls. New i18n keys `games.tictactoe.{name,shortDescription,rules}` (rules text varies by `boardSize` — three rule variants under one key namespace, or three sibling keys, decide in PR). `/games/tictactoe-{3x3,6x6,9x9}` slugs **301 to `/games/tictactoe?size={3,6,9}`** for a 2-week redirect window so invite links sitting in chats keep resolving. Sitemap regenerated; old slugs dropped from `sitemap.ts` (the 301 keeps them addressable, but they shouldn't be advertised as canonical). Old `games.tictactoe-{3x3,6x6,9x9}.*` i18n keys remain in this PR.
- **PR3 — delete the old three modules.** Lands no sooner than 2 weeks after PR2 to honour the redirect window. Removes domain projects (`PlayMe.Domain/Games/TicTacToe{3x3,6x6,9x9}/`), the matching `apps/web/features/games/tictactoe-{3x3,6x6,9x9}/` folders, DI registrations, the redirect rules, the old i18n keys, and the old catalog entries.

Cross-cutting doc touches (land inside the sprint PRs, not after):
- [`platform.md`](platform.md) §2 catalog table: collapse the three TTT rows. §1 #3 time-limit defaults: note that per-game defaults live in the module now, not the doc.
- [`CLAUDE.md`](CLAUDE.md) §1 catalog mention: "Tic-Tac-Toe 3×3 / 6×6 / 9×9" → "Tic-Tac-Toe (configurable board size: 3×3 / 6×6 / 9×9)".
- §2 below: revise the "New games are net-new modules, not parameterizations of existing ones" line — net-new modules remain the default for genuinely different games, but per-game configurable knobs (board size, variant toggles) now go through `gameOptions` on the existing module rather than spawning siblings. Also update "fixed at four modules for MVP" — stale since Sprint 8.

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
- **Size-driven clock defaults dropped from scope.** The plan called for `boardSize: 3 → 3 min, 6 → 3 min, 9 → 10 min` defaults on the configure page; there's no host-side clock picker today, so the module's single `DefaultClockBudget = 3 min` is the only knob. Listed under §3 deferred polish.

### 1.1 Roadmap rules

- **The first end-to-end slice (Sprint 1) is the canary** for whether the platform layer is right. Don't add anything to a later sprint until Sprint 1 ships.
- **No game-module work before Sprint 1.** Stub everything; defer until the platform skeleton is real.
- **Adding a new game (Sprint 3 onward) must be a pure addition.** If you modify the platform to add a game, fix the seam, then continue.
- **A sprint always lands a deployable, demonstrable slice.** Never split a sprint into "build, then make it work."
- **Sprint 7 is non-negotiable before public launch.** Going live without rate limits, CSP, or error monitoring is how products eat dirt.

---

## 2. Open questions / deferred to v2

Intentionally unresolved — raise in PRs rather than choosing silently.

- **Native mobile app** (React Native + Expo). Deferred to v2. `packages/shared` is already structured to be consumed by RN when added.
- **Monetization.** No monetization in v1. When introduced, the likely path is rewarded video ads first, then cosmetic IAP — both of which will require introducing optional accounts.
- **Accounts & player stats.** Not in v1 (pure anonymous play). Will become necessary when monetization, leaderboards, friends, or persistent history land.
- **Spectator mode.** Dropped from v1. Revisit after the core 1v1 flow is solid.
- **More games.** Catalog currently ships three modules (`tictactoe`, `connect4`, `reversi`). Net-new games remain the default — own state shape, own move payload, own win/draw detection, own renderer. **Per-game configurable variants** (board size, ruleset toggles) now go through `gameOptions` on the existing module rather than spawning sibling gameIds — the `tictactoe` consolidation in Sprint 9 set that pattern. Reach for a new module when the rules genuinely diverge; reach for a `gameOptions` knob when only a parameter does.
- **Push notifications.** Web push only (where supported) when re-engagement becomes a priority. Native push waits for the mobile app.
- **Tournaments / prizes.** Not in v1. If pursued later, legal review is required (Georgian gambling-law implications even for skill-based paid entry).
- **Managed log/trace backend.** OTel currently exports to stdout/file. Wire to Grafana Cloud / Honeycomb / similar when scaling beyond one API instance.
- **Secrets vault.** Currently env vars on App Service / Vercel. Once secret count or rotation frequency justifies it, move the API to Azure Key Vault (managed identity → API → Key Vault). Until then, env vars are acceptable.
- **WAF / DDoS.** No dedicated WAF in v1. Cloudflare already fronts `api.playme.ge` (for TLS reasons — see [`deployment.md`](deployment.md) §6.1), which gives us free baseline DDoS protection on the API path. Vercel fronts the web. If abuse traffic shows up beyond what those handle, escalate to Cloudflare WAF rules or Azure Front Door and re-evaluate rate-limit thresholds in [`security.md`](security.md) §5.
- ~~**On-call channel.**~~ Resolved: email. Sentry and Azure Monitor both route to the address configured in `infra/provision.env` (`ALERT_EMAIL`). Documented in [`security.md`](security.md) §11. Revisit if/when a team forms — Slack or a paging service makes more sense above one operator.

When a decision is made, update the relevant doc in the same PR.

---

## 3. Deferred polish / known follow-ups

In-scope-for-v1 items that have been deliberately postponed but should be picked up before launch (or shortly after). Distinct from §2 "deferred to v2" — these are smaller polishes that don't change scope, just timing. Pick from this list when there's bandwidth between sprints.

- **Disconnect-grace countdown in the UI.** Sprint 5 wired the tiered abandon-grace server-side ([`platform.md`](platform.md) §1 #7) — the server auto-ends the match with `Outcome.Disconnect` after the grace elapses. The still-connected player today only sees a muted "Opponent disconnected." hint. A visible countdown ("Match ends in 1:23") would let them decide whether to wait it out. Two designs surveyed: (A) extend the `OpponentDisconnected` event payload with the grace deadline + add `OpponentGraceStarted` for the turn-flip case (~100 LOC, ephemeral state); (B) store `HostGraceDeadline` / `ChallengerGraceDeadline` on the `Room` aggregate so every `RoomDto` snapshot carries them (~200 LOC, watertight across reconnects). Suppress entirely for the 1-min "no grace tier."

- **Host clock-picker + size-driven `tictactoe` defaults.** The configure page has no host-side time-limit picker today; the room inherits the module's `DefaultClockBudget`. Sprint 9 dropped the planned `boardSize: 3 → 3 min, 6 → 3 min, 9 → 10 min` size-driven preselect because there's nothing for it to *pre*-select — adding the 1/3/10-min segmented control (the platform invariant in [`platform.md`](platform.md) §1 #3) unblocks both the per-game default and per-room time-limit selection in one go. Likely needs a new `timeLimit` field on `CreateRoomCommand` / `Room` / `RoomDto` plus the segmented control on the configure form, mirroring the side-mode picker.

When picking up an item, move it under the relevant sprint header or open a feature branch directly.
