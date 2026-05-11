# CLAUDE.md

This file gives Claude Code the context it needs to work effectively in the **PlayMe** repository. Read it before suggesting changes, generating code, or running commands.

---

## 1. Product Overview

**PlayMe** (playme.ge) is a real-time, anonymous, two-player casual games platform. A host creates a match, picks the game and options, gets a shareable invite link, and sends it to a friend. The challenger opens the link, both players join the room, and they play live over the network with a per-player chess clock.

Key product facts:

- **No accounts.** Players are anonymous; identity is a display name typed at room entry. No signup, no persistence of player history in v1.
- **Two players only.** Every match is exactly host + challenger. No spectators in v1.
- **Real-time only.** Both players are connected concurrently; moves are pushed via WebSockets (SignalR). No async / play-by-mail mode.
- **Invite-link matchmaking only.** No public matchmaking queue, no friends list, no leaderboards in v1.
- **Catalog is fixed and small (MVP).** Tic-Tac-Toe 3×3, Tic-Tac-Toe 6×6, Tic-Tac-Toe 9×9, Connect 4. Each game is its **own self-contained module** — no shared rules engine. Common features live in the platform layer, gameplay code does not.
- **Languages at launch:** Georgian (`ka`) and English (`en`).
- **No monetization in v1.** Future consideration only; not in scope for the codebase yet.
- **Client surface in v1: web only.** Responsive Next.js app with PWA support. Native mobile (React Native / Expo) is deferred to v2 — but the repo is structured so adding it later is cheap.

---

## 2. Architecture

### 2.1 System architecture

```
                ┌──────────────────┐
                │   Vercel (CDN)   │
                │   Next.js (SSR)  │  ← web client, responsive + PWA
                └────────┬─────────┘
                         │ HTTPS + WSS (SignalR)
                         ▼
                ┌──────────────────┐
                │ Azure App Service│
                │  ASP.NET Core    │  ← stateless, horizontally scalable
                │  (.NET 10 LTS)   │
                └────────┬─────────┘
                         │
                         ▼
                ┌──────────────────┐
                │  Azure Cache for │
                │      Redis       │  ← state store + SignalR backplane
                └──────────────────┘

   Observability: Sentry (errors) + PostHog (analytics) + Serilog/OTel (logs)
```

- The **API is stateless**. All match/room state — board, clock, players, presence — lives in Redis. Any API instance can serve any client; no sticky sessions required.
- **Redis serves two roles:** primary state store (keyed by room id) and **SignalR backplane** via pub/sub. One Redis instance, two responsibilities, namespaced channel prefixes.
- **SignalR** is the only real-time transport. Clients open one SignalR connection per active match.
- The **server is the single source of truth** for game state and the clock. Clients render what the server tells them; client moves are *proposals* that the server validates and commits.

### 2.2 Repository layout (monorepo)

```
/
├── apps/
│   ├── api/         # ASP.NET Core Web API (.NET 10)
│   └── web/         # Next.js (App Router) — SSR + SEO + PWA
├── packages/
│   ├── shared/      # TS types, Zod schemas, generated API client, SignalR wrapper, i18n keys
│   └── config/      # eslint, tsconfig, prettier base configs
├── infra/           # Redis docker-compose, Dockerfiles, deploy configs
├── tools/           # codegen scripts (OpenAPI → TS client), maintenance utilities
├── pnpm-workspace.yaml
├── turbo.json
└── CLAUDE.md
```

- `apps/mobile/` is **deferred to v2** (React Native + Expo). Do not create it yet, but design shared code so it can be added without a refactor.
- `packages/shared/` exists in v1 even though only `apps/web/` consumes it. It's the foundation for cross-platform code sharing when mobile lands.
- `infra/` holds the `docker-compose.yml` for local Redis, the API `Dockerfile`, and any deploy descriptors. Redis is never run via a separate code module — it's pure infrastructure.

### 2.3 Platform vs game-module split

Every game shares a **platform layer**. Game code never reimplements platform features. Each game is a **self-contained module** for its rules and UI.

**Platform layer (shared, one implementation):**

1. Room lifecycle — creation, single-use invite link, TTL, cleanup.
2. Two-role model — host and challenger, exactly two players per room.
3. Chess clock — fixed total per player, server-authoritative, ticks server-side. **Three presets only** (no custom values, no "unlimited" in v1): **1 min**, **3 min**, **10 min** per player. Both players always get the same time bank. Modeled as a strongly-typed enum `TimeLimit { OneMin, ThreeMin, TenMin }` on the API; FluentValidation rejects anything else with `errors.config.invalidTimeLimit`. Room state stores the resolved `timeLimitMs` for the clock model in §2.9. **Per-game defaults** preselected on the configure page so the host can submit fast: `tictactoe-3x3` → 3 min, `tictactoe-6x6` → 3 min, `tictactoe-9x9` → 10 min, `connect4` → 3 min. All three presets remain selectable for every game — defaults are just the preselected option.
4. Online move pipeline — every move travels client → server → opponent via SignalR.
5. Host-created matches — host chooses game type, time limit, side/color, display name.
6. Invite flow — host shares the link; first non-host to open it becomes the opponent.
7. Connection-loss tolerance — applies to the `InProgress` state. Short reconnect grace (default 30 s); **the clock keeps running during disconnect** (no pause). Reconnect within grace and the player rejoins the match seamlessly. **The grace is a UX threshold, not a hard cutoff.** When 30 s elapses without reconnect, the server emits **`OpponentAbandoned`** to the still-connected player, who then has three ways to end the match: (a) call **`ClaimVictory()`** → `Outcome.Disconnect(opponent)`; (b) wait for the disconnected player's clock to run out → `Outcome.Timeout(opponent)`; (c) resign → `Outcome.Resign(self)`. The disconnected player **may reconnect at any time before the match formally ends** — their seat isn't locked when grace expires, only the connected player's escape-hatch UI changes. If reconnect and `ClaimVictory()` race, the §2.8 room lock serializes them; whichever lands first wins. **Disconnect timing is anchored to the SignalR disconnect moment, not to whose turn it is.** A player may disconnect while inactive; the grace timer starts at disconnect and does not reset when their turn comes around. The still-connected active player can submit moves normally while the opponent is offline — move acceptance gates on `room.Status == InProgress` and "caller is the active player," not on opponent presence. The clock ticks against whichever player is currently active per the lazy model in §2.9, including when that player is offline. Disconnects during `WaitingForOpponent` follow a different rule (§2.9): they are transparent, governed by the room TTL rather than a short reconnect window, because there is no clock to enforce.
8. Resign — always behind an explicit confirm step to prevent accidental clicks.
9. Post-match handling — winner/loser can offer rematch or exit to lobby.
10. Rematch handshake — either player can offer a rematch from the `Ended` state. The server serializes `OfferRematch` calls via the §2.8 room lock, so **only one offer is active at a time**. First offer wins: it transitions `Ended → AwaitingRematch` and records the offerer; the other player's UI then shows **Accept / Reject** (replacing their own "Offer rematch" button). A second `OfferRematch` from the opponent (near-simultaneous clicks) is treated as an **implicit accept** — the room transitions to `InProgress` with swapped sides per §2.3 #15. `AcceptRematch` from the responder has the same effect. `RejectRematch` is valid **only for the responder** (not the offerer); the rejector auto-returns to the lobby, the offerer stays in the room with a "rematch declined" notice and a manual "Back to lobby" button. Cancelling your own offer is **not** a v1 feature — once offered, the only exits are opponent-accept, opponent-reject, opponent-exit, or the offerer manually exiting (via the "Back to lobby" button, which calls `ExitRoom()` per the invariants in §2.9).
11. First-move ownership — determined per game by the canonical rule (X first in Tic-Tac-Toe, **Red first in Connect 4**). The player assigned the first-move side moves first, regardless of how the assignment happened (host's specific choice, server-random, or challenger-picked per §2.3 #14).
12. Clock-start rule — clock for the side that moves first **starts immediately** when both players are present in the room. No "ready up" step.
13. Rematch-series scoring — while both players stay in the same room across rematches, the server tracks a session-only scoreboard. **Scoring rule:** Win = 1 point, Draw = 0, Loss = 0 (win-only; the user's chosen rule, not chess-style 1/½/0). **Schema:** `seriesScore: { host: int, challenger: int, draws: int }` — `host` and `challenger` count their wins; `draws` is shared (not per-player) and tracked for display context, not scoring. **Outcome mapping:** `Win` → opponent's loser-side stays unchanged, winner side `+= 1`. `Draw` → `draws += 1`. **`Resign`, `Timeout`, and `Disconnect` roll into the opponent's win** — they're not separate score categories, since from a player's perspective "I won that game" reads the same whether the other side ran out of time, gave up, disconnected, or got beaten on the board. **Display:** primary score line is the win count (`Lado 2 — 1 Nika`); if `draws > 0`, append a small subtitle (`1 draw`). The total matches played is `host + challenger + draws`. The scoreboard is server-authoritative, lives in the room state in Redis, and is discarded when the room reaches `Closed` or `Expired`. No persistence beyond the room.
14. Side/color selection — at room creation the host chooses one of **three options** for the side/color split: (a) **specific side** — host picks their own side (X or O for Tic-Tac-Toe; red or yellow for Connect 4); challenger automatically gets the other. (b) **Random** — server picks the host's side at room creation and the challenger gets the other; both players see their assignment as read-only info. (c) **Let challenger pick** — sides remain unresolved until the challenger's join-onboarding step, where they select one of the two available sides and the host gets whichever they don't pick. In all three options, **both sides are fully resolved before the room transitions to `InProgress`**, which keeps platform invariant #12 (clock starts immediately when both players are in the room) intact. The room state stores `hostSide` and `challengerSide`; under option (c) both are `null` until the challenger picks.
15. Rematch side swap — on every accepted rematch (transition from `AwaitingRematch` back to `InProgress` for a new match within the same room), the server **swaps `hostSide` and `challengerSide` deterministically**. Whoever had X last match plays O; whoever had red plays yellow. Applies regardless of how sides were originally chosen (§2.3 #14). **Rationale:** first-move advantage is real (especially on 9×9 Tic-Tac-Toe and in Connect 4); alternating sides across a rematch series makes the per-player scoreboard (§2.3 #13) reflect playing strength rather than side luck. The swap is automatic — no joiner re-prompt — and the `MatchStarted` event for the new match carries the swapped assignments and who moves first. UI must clearly display each player's *current* side in a persistent HUD slot, since it changes match-to-match within a session.

**Game modules (independent, no shared engine):**

| Module | Game |
|---|---|
| `tictactoe-3x3` | Tic-Tac-Toe on 3×3, win = 3 in a row |
| `tictactoe-6x6` | Tic-Tac-Toe on 6×6, win = 4 in a row |
| `tictactoe-9x9` | Tic-Tac-Toe on 9×9, win = 5 in a row |
| `connect4` | Connect 4 on 7×6 with gravity, win = 4 in a row. Colors: **red** and **yellow** (traditional pair). |

Each module owns: its board representation, legal-move validation, win/draw detection, and its UI rendering. **Do not extract a shared rules engine across modules** — that decision is intentional. Common features live only in the platform layer.

#### Game rules (canonical spec)

These are the authoritative rules. The server validates every move against them. Per-module READMEs (`apps/api/src/PlayMe.Domain/Games/<game>/RULES.md`) may expand on edge cases, but the canonical statement lives here.

**`tictactoe-3x3`** — 3×3 grid, players alternate placing X / O. First to align **3 consecutive** marks (horizontal, vertical, or either diagonal) wins. Board fills with no line → **draw**. **X moves first.** No wraparound.

**`tictactoe-6x6`** — 6×6 grid, players alternate placing X / O. First to align **at least 4 consecutive** marks (horizontal, vertical, or either diagonal) wins. A run of 5 or 6 in a row counts as a win, not separately. Board fills with no line → **draw**. **X moves first.** No wraparound. **No swap / pro / balancing rule** in v1.

**`tictactoe-9x9`** — 9×9 grid, players alternate placing X / O. First to align **at least 5 consecutive** marks (horizontal, vertical, or either diagonal) wins. Board fills with no line → **draw**. **X moves first.** No wraparound. **No swap / pro / balancing rule** in v1 (i.e. plain Gomoku-style first-to-5; we know first-player advantage exists on 9×9 and accept it for casual play).

**`connect4`** — 7-column × 6-row board with **gravity**: a dropped disc occupies the lowest empty cell of the chosen column. Players alternate dropping **red** and **yellow** discs. First to align **4 consecutive** discs (horizontal, vertical, or either diagonal) wins. A column with no empty cells is not a legal target. Whole board fills with no line → **draw**. **Red moves first** by Hasbro convention; the host's color choice at room creation therefore implicitly decides who starts (platform rule §2.3 #11).

**Connect 4 piece rendering (accessibility).** Red and yellow are perceptually close for the most common forms of color-blindness (deuteranopia / protanopia, ~5% of male players), so the two sides must be distinguishable without relying on hue alone. Render **red as a solid disc** and **yellow as a ring (donut)** — same outer circle, yellow has a transparent inner hole. This preserves Connect 4's "stacked discs" visual identity, keeps both sides symmetric in shape, and remains legible in monochrome, high-contrast mode, screenshots, and at small mobile sizes. The win-line highlight should glow around both shapes equally. Do **not** distinguish the two players by changing the outer shape (e.g. circle vs. triangle) — that breaks the gravity/stacking intuition that defines Connect 4.

**Rules shared by all four games:**

- A move that lands in an occupied cell (Tic-Tac-Toe) or a full column (Connect 4) is **rejected by the server**, the player's clock keeps running, and the client must surface a clear inline error — not silently retry.
- Win detection runs **after every accepted move**, on the server. The server emits an `MatchEnded` event with the winning line coordinates so the client can highlight them.
- Resign and timeout are platform-level outcomes (see §2.3 #8 and #3); they are not game-rule terminations.

#### Cross-game in-match UX rules

These apply to every game module's board rendering, regardless of which game it implements.

- **Last-move highlight.** Every accepted opponent move must be visually highlighted on the board for the receiving player — e.g. a subtle pulse, glow, or coloured border on the just-played cell (Tic-Tac-Toe) or the disc that just landed (Connect 4). The highlight persists until the receiving player makes their own next move, then disappears. This matters especially on the 6×6 and 9×9 boards, where scanning for a single new mark is slow. The player's *own* last move does not need this highlight; the focus is on making the opponent's action obvious.
- **Winning-line highlight.** On match end with `Outcome.Win`, the winning line is highlighted with a **distinct** visual treatment from the last-move highlight (e.g. solid glow along all winning cells vs. the pulse on a single cell). The server provides the coordinates in `MatchEnded`; the client renders them; do not recompute the winning line on the client.
- **Series scoreboard.** When platform invariant §2.3 #13 applies (rematches in the same room), the in-match UI displays the current score for both players in a fixed, glanceable location (typically beside or above each player's clock).

### 2.4 Backend architecture (apps/api)

- **ASP.NET Core Web API** with **full controllers** (`ControllerBase` + `[ApiController]` attribute routing). **No minimal APIs.** Controllers give us clearer per-endpoint discoverability, attribute-based authorization, model binding, filters, and route organization as the API grows.

#### Clean Architecture

The API follows **Clean Architecture** with strict layer boundaries. **The dependency rule is non-negotiable: dependencies point inward only.** Outer layers depend on inner layers; inner layers know nothing about outer ones.

```
                ┌─────────────────────────────────────┐
                │  Api  (controllers, hubs, DI, mw)   │  ← outermost
                └──────────────────┬──────────────────┘
                                   │ depends on ↓
                ┌──────────────────▼──────────────────┐
                │  Infrastructure  (Redis, Sentry,    │
                │  OTel, external clients)            │  ← implements Application interfaces
                └──────────────────┬──────────────────┘
                                   │ depends on ↓
                ┌──────────────────▼──────────────────┐
                │  Application  (use cases, ports,    │
                │  DTOs, validators)                  │
                └──────────────────┬──────────────────┘
                                   │ depends on ↓
                ┌──────────────────▼──────────────────┐
                │  Domain  (entities, value objects,  │
                │  rules, domain events)              │  ← innermost, pure C#
                └─────────────────────────────────────┘
```

Concretely:

- **`Domain/`** — pure C# only. Entities (`Room`, `Clock`, `Match`), value objects (`RoomId`, `Move`, `TimeBank`), per-game rules (`Domain/Games/<game>/`), and domain events. **Zero references** to ASP.NET, Redis, Serilog, Sentry, or any NuGet package outside the BCL. Game-module rules live here as self-contained units (no shared rules engine).
- **`Application/`** — use cases / handlers (one class per use case, e.g. `CreateRoomHandler`, `JoinRoomHandler`, `SubmitMoveHandler`), **ports** (interfaces like `IRoomRepository`, `IClockService`, `IClock` for time, `IAnalytics` for PostHog), DTOs (`record` types), and validation. The application layer **defines** the interfaces; it never implements them. Light CQRS-style separation: `Commands/` for state-changing use cases, `Queries/` for reads. **No MediatR** — handlers are plain classes resolved via DI.
- **`Infrastructure/`** — adapters that **implement** the ports defined in `Application`. `RedisRoomRepository`, `SystemClock`, `PostHogAnalytics`, `SentryErrorReporter`, `OtelTracing`. This layer is allowed to depend on `Application` (to implement its interfaces) and on third-party libraries (StackExchange.Redis, etc.).
- **`Api/`** — controllers, SignalR hubs, DI composition root (`Program.cs` / extension methods), middleware, model binding, problem-details translation. Controllers and hubs are **thin**: validate input, call an `Application` handler, map the result to a response DTO. **No business logic in controllers or hubs.**

Rules that follow from the dependency direction:

- **DI** via the built-in container. Composition lives in `Api/DependencyInjection/` extension methods (one per layer: `AddDomain()`, `AddApplication()`, `AddInfrastructure()`, `AddApi()`). Do not introduce MediatR / Autofac / etc. without discussion.
- **DTOs** are the contract surface (records). Never expose `Domain` entities directly through controllers or hubs.
- **One SignalR hub: `RoomHub`** at `/hubs/room`, under `apps/api/src/PlayMe.Api/Hubs/RoomHub.cs`. Single hub, not split — all real-time operations (join, move, resign, rematch offer/accept/reject, exit) are room-scoped and share the same session. Hub methods are thin: parse input, validate via FluentValidation, delegate to an `Application` handler, return/broadcast a DTO. **No game rules in hubs, ever.** If the class grows large, split it via C# partial classes — never via additional Hub types.
- **Redis access** is encapsulated in `Infrastructure/Redis/` via a typed repository per concern (room repo, clock repo, etc.). The `IRoomRepository`-style interface lives in `Application/`; the Redis implementation lives in `Infrastructure/`. No raw `IConnectionMultiplexer` use in `Application` or `Domain`.
- **External services** (Sentry, PostHog, OpenTelemetry exporters) are wrapped behind interfaces in `Application/` and implemented in `Infrastructure/`. The rest of the code never imports their SDKs directly.
- **Time** is injected as `IClock` (`Application/Abstractions/IClock.cs`) with a `SystemClock` implementation in `Infrastructure/`. Domain logic that depends on time (the chess clock) takes `IClock` as a dependency. Never call `DateTime.UtcNow` in `Domain` or `Application`.

Project structure (one `.csproj` per layer):

```
apps/api/
├── src/
│   ├── PlayMe.Domain/          # no project refs
│   ├── PlayMe.Application/     # refs Domain
│   ├── PlayMe.Infrastructure/  # refs Application (+ Domain transitively)
│   └── PlayMe.Api/             # refs Application + Infrastructure
├── tests/                      # mirrors src/ (Domain.Tests, Application.Tests, ...)
└── PlayMe.sln
```

Enforce the dependency rule via project references — if `Domain.csproj` ever has a `<ProjectReference>` other than nothing, that's a bug.

#### RoomHub method index

Semantic index of all Hub methods. **Literal C# signatures live in `RoomHub.cs`** — this table is the source-of-truth for *what methods exist and when each is valid*, not for their exact parameter types (those are C# records in `Application/`). Every method runs the authorization check from §5.4 before any other logic.

| Method | Valid when | Effect | Server emits |
|---|---|---|---|
| `JoinRoom` | On SignalR connect; room in `WaitingForOpponent`, `InProgress`, `Ended`, or `AwaitingRematch` | Registers presence; reattaches via session cookie | `OpponentJoined` (challenger's first join), `OpponentReconnected` (reconnect during `InProgress`) |
| `SubmitMove` | Room `InProgress`; caller is active player; effective clock > 0 | Validates move via Domain rules; applies; flips turn; reschedules timeout | `MoveAccepted` (both), `MoveRejected` (caller only), `MatchEnded` if win/draw |
| `Resign` | Room `InProgress`; caller is in the match | Ends match | `MatchEnded(Outcome.Resign(caller))` |
| `ClaimVictory` | Room `InProgress`; opponent disconnected; §2.3 #7 grace expired | Ends match | `MatchEnded(Outcome.Disconnect(opponent))` |
| `OfferRematch` | Room `Ended` (creates offer) OR `AwaitingRematch` from responder (implicit accept per §2.3 #10) | Records offer or starts new match | `RematchOffered`, or `MatchStarted` on implicit accept |
| `AcceptRematch` | Room `AwaitingRematch`; caller is responder (not offerer) | Starts new match with swapped sides per §2.3 #15 | `MatchStarted` |
| `RejectRematch` | Room `AwaitingRematch`; caller is responder | Closes the room; rejector auto-routed | `RematchDeclined` to offerer |
| `ExitRoom` | Room `Ended` or `AwaitingRematch` | Transitions room to `Closed` | `OpponentExited` to the still-present player |

When adding a new Hub method: append a row here, then implement.

### 2.5 Frontend architecture (apps/web)

- **Next.js (App Router)**, React Server Components where appropriate, TypeScript strict mode.
- SEO is a hard requirement — **prefer SSR/SSG for public pages**, CSR only for the in-match UI.
- **PWA support is part of v1**: web manifest, icons, install prompt, optional service worker for offline shell.
- **Responsive design** is the only mobile story in v1. Touch targets, screen-on (Wake Lock API during a live match), and iOS Safari quirks must be handled by web alone.
- Architectural pattern: **feature-sliced**. A feature owns its UI, hooks, and data access. Cross-cutting concerns (i18n, telemetry, SignalR connection) live in their own modules consumed by features.
- All API calls go through the **generated client** in `packages/shared`. Do not hand-roll `fetch` calls in features.

**Key pages.**

- **Landing (`/`)** — public, SSR. Renders the full game catalog as a **grid of large cards**, not a list. One card per game module (Tic-Tac-Toe 3×3 / 6×6 / 9×9, Connect 4). Each card shows the game name, a localized short description, and a small visual preview of the board. Clicking a card takes the user into the configuration flow for that game. The grid should remain visually balanced for any number of games (1 → 12+) without restructuring, since the catalog will grow post-MVP. **The landing page also includes a concise "How PlayMe works" section** below the catalog (or as an adjacent panel): 3–4 short steps explaining that the host picks a game and time limit, gets a link to share, and that the friend who opens the link becomes the opponent. Keep it visual (icons / numbered steps), localized to `ka` and `en`, and short enough to scan in under 10 seconds — first-time visitors must understand the product without scrolling for instructions.
- **Configure (`/play/<game>`)** — single URL per game (slug equals the module ID from §2.3; e.g. `/play/tictactoe-3x3`, `/play/connect4`). This page is both the SEO content surface for that game and the host's configure form — there is no separate `/new` route. Host picks **time limit, side/color, display name**. Time limit is one of three presets per platform invariant §2.3 #3: **1 / 3 / 10 min** (segmented control or radio group, not free-form input). The preset preselected on page load is the game-specific default from §2.3 #3. Side/color is one of three explicit options per platform invariant §2.3 #14: (a) a specific side, (b) random (server picks at creation), or (c) let challenger pick. Submitting calls `POST /api/rooms` and routes the host to `/r/<roomCode>`. **The page must also expose the rules of the selected game** via either a "Rules" tab beside the configuration form or an inline "How to play" expandable panel — whichever fits the layout better.
- **Room / Match (`/r/<roomId>`)** — CSR. Holds the SignalR connection, renders the chosen game's board, the per-player clock, the resign button (with confirm), the post-match overlay (rematch offer / accept / reject / exit), and an always-accessible **"Rules" button/overlay** so either player can re-read the rules mid-game without leaving the room.
- **Join (`/r/<roomId>`, no host token)** — same URL as the host's room link; the server distinguishes by role. Renders the joiner onboarding: **display name (required)** plus, only if the host chose "let challenger pick" (§2.3 #14 option c), a **side picker** showing the two available sides. Under options (a) and (b) the challenger's assigned side is shown as read-only info, not as an input. The page also shows the same **"Rules" panel/tab** as the configure page so the challenger can read the rules *before* joining the match. Submitting transitions into the same match view.

  **Join contract.** Registration is a **single atomic API call**: `POST /api/rooms/{code}/join` with body `{ displayName, side? }`. `displayName` is always required (sanitized per §5.3, ≤ 24 chars). `side` is conditional based on the room's stored side-selection mode (set at room creation per §2.3 #14): under modes (a) and (b) it MUST be `null` (host's choice already fixed both sides — sending a value is rejected with `errors.join.sideNotAllowed`); under mode (c) it MUST be one of the two valid values for the game (missing → `errors.join.sidePickRequired`; invalid → `errors.join.invalidSide`). The handler runs inside the §2.8 room lock: verifies the room is `WaitingForOpponent` and the challenger seat is empty (otherwise `409 errors.room.alreadyJoined` for single-use-link reuse, or `410 errors.room.expired` / `errors.room.closed`), generates the `challengerPlayerId` (crypto-RNG per §5.4), sets `challengerDisplayName`, sets `challengerSide` and `hostSide` under mode (c), refreshes the room TTL, issues the HttpOnly session cookie, and returns. **No separate "submit side" endpoint** — splitting registration would create a half-state (challenger registered but side undecided) that other handlers would have to defensively guard against.

**Game rules surface (shared content).** Rules are reachable from **three places**: (1) the **Configure page** for the host, (2) the **Join page** for the challenger before they commit to a match, and (3) the **in-match UI** for either player who needs to look something up. All three surfaces consume the **same localized rules catalog** — one entry per `GameId`, with `ka` and `en` translations — and must match the canonical spec in §2.3. There is one source of truth; do not hand-write rules text per page.
- **404 / Expired room** — friendly fallback for dead invite links.

**SEO.** SEO is a **hard requirement** for the public surface. Treat it as a first-class concern, not a polish step.

- **Rendering strategy.** Public pages (landing, per-game configure pages, "about" / static content) are **SSR or SSG** — never client-only. Match/room pages (`/r/<roomId>`) are CSR and **must not be indexable**.
- **Metadata.** Use the Next.js **Metadata API** (App Router) on every public route. Every public page exports `metadata` or `generateMetadata` with: localized `title`, `description`, canonical URL, Open Graph (`og:title`, `og:description`, `og:image`, `og:locale`), Twitter Card (`summary_large_image`), and `robots`. Per-game configure pages get game-specific titles ("Play Connect 4 online with a friend — PlayMe").
- **Internationalization (hreflang).** Two locales: `ka` (default, at root) and `en` (under `/en`). **Decided — do not revisit without discussion.** Concretely: `/`, `/play/<game>`, … are Georgian; `/en`, `/en/play/<game>`, … are English. Room URLs (`/r/<roomCode>`) are locale-agnostic — the match UI reads the user's locale from the cookie. **No automatic `Accept-Language` redirect from `/` to `/en` in v1**; the global header has a visible language switcher, and the locale choice is persisted in a cookie + `localStorage`. Every public page emits `<link rel="alternate" hreflang="ka" />`, `<link rel="alternate" hreflang="en" />`, and `<link rel="alternate" hreflang="x-default" />` (pointing at the `ka` URL).
- **Canonical URLs.** Every public page sets `<link rel="canonical" />` to its canonical locale-aware URL. Query strings used for tracking (`utm_*`, etc.) must not pollute the canonical.
- **Indexing rules.** Public pages: indexable. `/r/<roomId>` and any flow downstream of it (`/r/<roomId>/...`): **`noindex, nofollow`** via `metadata.robots` *and* the `robots.ts` rules. OG tags on `/r/<roomId>` are still set (so social previews look nice when an invite link is shared), but search engines do not crawl them.
- **Sitemap & robots.** `apps/web/app/sitemap.ts` and `apps/web/app/robots.ts` generate `sitemap.xml` and `robots.txt` at build time. The sitemap includes the landing page and every per-game configure page in **both locales**, with `<xhtml:link rel="alternate">` entries for hreflang pairing. Room URLs are excluded.
- **Structured data (JSON-LD).** Landing emits `WebSite` and `WebApplication` JSON-LD. Each per-game configure page emits a `Game` schema with `name`, `description`, `numberOfPlayers: 2`, and `gameLocation: { @type: "VirtualLocation", url: "..." }`. Use `next/script` with `strategy="afterInteractive"` or render inline `<script type="application/ld+json">` from a server component.
- **Performance / Core Web Vitals.** Targets: **LCP ≤ 2.5s**, **INP ≤ 200ms**, **CLS ≤ 0.1** at p75 on mobile 4G. Use `next/image` for any image asset, `next/font` for fonts (eliminates FOUT/CLS), avoid layout shift in the catalog grid by reserving aspect-ratio for card previews. Audit with Lighthouse + Vercel Speed Insights before any release.
- **URLs.** Descriptive and stable: `/`, `/en`, `/play/tictactoe-3x3`, `/play/connect4`. Slug equals the module ID from §2.3 so there's one identifier per game across code, URLs, room state, analytics, and the rules catalog. Never expose internal IDs in public URLs. Room codes in `/r/<roomId>` are opaque short tokens, not sequential IDs (so they're unguessable).
- **PWA manifest.** `manifest.webmanifest` includes localized `name` and `short_name`. The default locale of the manifest is `ka`; provide an `en` manifest if Next.js doesn't auto-localize.

**Theming (dark / light mode).** First-class part of the web client.

- **Three modes exposed to the user:** `light`, `dark`, `system`. The toggle lives in the global header (and in any settings drawer); it cycles through all three, not a binary switch.
- **Default is `system`** — first-time visitors follow their OS preference via `prefers-color-scheme`. No popup, no onboarding step.
- **User override is persisted** — when a user picks `light` or `dark`, store the choice in `localStorage` (key `playme.theme`). When they pick `system`, clear the key so future visits re-read the OS preference. A theme cookie is also set (`playme.theme=<value>`, `SameSite=Lax`) so the SSR layer can render with the correct initial theme — see below.
- **No flash of wrong theme (FOUC).** Public pages are SSR, so the server doesn't natively know the user's choice. Read the `playme.theme` cookie in the root layout and emit `<html data-theme="...">` server-side; combine with a tiny inline blocking script in `<head>` that resolves `system` to actual `light`/`dark` from `prefers-color-scheme` before paint. This must run *before* any styled content paints, otherwise the page flashes.
- **Implementation:** use **`next-themes`** with the App Router adapter; it handles the cookie/localStorage sync, the SSR hydration mismatch, and the `system` resolution. Don't roll a custom theme manager unless `next-themes` proves insufficient.
- **CSS strategy:** **semantic color tokens** only, never raw hex/RGB in components. Define tokens (`--color-bg`, `--color-fg`, `--color-surface`, `--color-accent`, `--color-board-line`, `--color-piece-x`, `--color-piece-o`, `--color-connect4-red`, `--color-connect4-yellow`, etc.) in `app/globals.css` under `[data-theme="light"]` and `[data-theme="dark"]` selectors. Components reference tokens via CSS variables (or Tailwind's `bg-surface text-fg` if Tailwind is wired with a custom theme). Adding a third theme later (e.g. high-contrast) is then a token-set addition, not a component rewrite.
- **System-theme reactivity.** When mode is `system`, listen to the `(prefers-color-scheme: dark)` media query and update the active theme live — users change their OS theme mid-session (e.g. via macOS auto night).
- **Browser chrome.** `<meta name="theme-color">` is set per theme (different `content` for light vs dark), so the mobile address bar matches.
- **PWA manifest.** Provide `theme_color` and `background_color` for the default (light) install; the dark variant is handled at runtime, not via separate manifests.
- **Accessibility.** Token pairs must meet **WCAG AA contrast** (4.5:1 for body text, 3:1 for large text and UI components) in both modes. The chess-clock danger color and the win-highlight color need to be distinguishable from regular board state in **both** themes — verify with a contrast checker, not by eye.

### 2.6 Shared package (packages/shared)

- **TypeScript types** for game state, moves, room state, clock state.
- **Zod schemas** for validating any external input (route params, env vars, server-pushed messages).
- **Generated API client** from the API's OpenAPI document (`tools/gen-api`).
- **SignalR client wrapper** — typed wrapper around `@microsoft/signalr` with reconnect logic.
- **i18n catalogs** for `ka` and `en`. Translation keys are defined here so future mobile reuses them.
- **No React, Next.js, or React Native imports** allowed in `packages/shared`. Pure TS only.
- **DTO source of truth.** DTO shapes are owned by **C# records in `PlayMe.Application/`**. The generated TS client (`pnpm gen:api` consuming the API's OpenAPI document) is the canonical mirror for the web. Zod schemas in `packages/shared/src/schemas/` validate inputs at runtime (URL params, forms, server-pushed SignalR messages). CLAUDE.md describes *field-level contracts inline* only when the API surface is decision-worthy (e.g. the join body in §2.5); literal record definitions belong in code. **Do not transcribe DTO schemas into CLAUDE.md** — they go stale instantly.

### 2.7 Domain vocabulary

Pin canonical terms so different files don't invent synonyms. When the codebase refers to one of these concepts, use the term on the left, not a substitute.

| Term | Definition |
|---|---|
| **Room** | Container for one matchmaking session. Identified by `RoomCode`. Survives multiple matches if rematches are accepted. Has the lifecycle in §2.9. |
| **RoomCode** | Opaque, high-entropy URL token (≥128 bits) used as the room's public identifier. Unguessable. |
| **Host** | The player who created the room. Carries the `host` role in their session token. |
| **Challenger** | The player who joined via the invite link. Carries the `challenger` role. |
| **Player** | Generic term for "host or challenger." Anonymous, identified only by a display name typed at entry. |
| **DisplayName** | A player's chosen name for this session. Sanitized, ≤24 chars. Not persistent across sessions. |
| **PlayerId** | Crypto-random 128-bit opaque ID assigned to a player on session creation (host at room creation, challenger at join). Distinct from `RoomCode`. Stored in both the session token and the room state as `hostPlayerId` / `challengerPlayerId`. Used as the authorization second factor (token's `playerId` must match the stored value on every action) and for anonymous structured logging. Not global; lives only as long as the room. Survives rematches and side swaps within the same room. |
| **Match** | One round of gameplay inside a room. A room can contain multiple matches if rematches are accepted. |
| **Move** | A player's gameplay action (Tic-Tac-Toe: `{cell: int}`, Connect 4: `{column: int}`). Validated and accepted/rejected by the server. |
| **Clock** | Per-player remaining time. Server-authoritative; ticks server-side; broadcast on move and at low frequency. |
| **TimeBank** | The fixed total time allocated to each player at match start. Same for both players. |
| **Session** / **SessionToken** | Signed credential tying a single connection to a (room, player, role) triple. HttpOnly cookie or bearer. Dies with the room. |
| **GameId** | String identifying which game module is in play: `tictactoe-3x3`, `tictactoe-6x6`, `tictactoe-9x9`, or `connect4`. |
| **GameModule** | A self-contained per-game implementation (rules + UI). Lives in `Domain/Games/<game>/` on the API and as a feature folder on the web. |
| **Outcome** | Terminal match result: `Win` (with winner + winning-line coordinates), `Draw`, `Resign`, `Timeout`, or `Disconnect` (the still-connected player claimed victory after the §2.3 #7 reconnect grace elapsed). |
| **RematchOffer** | The post-match handshake state. Either player can offer; opponent accepts (→ new match) or rejects (→ rejector to lobby, offerer stays with a notice). |

Don't introduce alternative terms (`Game` for `Match`, `User` for `Player`, `Token` for `Session`) without updating this table.

### 2.8 Redis key schema

All keys are prefixed `playme:` to namespace the application. Use `:` as the segment separator — Redis tooling renders it as a tree.

| Pattern | Purpose | TTL |
|---|---|---|
| `playme:room:{roomCode}` | Full room state (players including `hostPlayerId` / `challengerPlayerId` (§2.7), `hostSide` / `challengerSide`, display names, status, current match, clock snapshot, last-tick timestamp, series scoreboard for rematches). Stored as a JSON string. | 30 min while `WaitingForOpponent`, 1 h while `InProgress` (refreshed on every interaction), 5 min after `Ended`. |
| `playme:room:{roomCode}:lock` | Distributed lock for atomic move processing (prevents racing `SubmitMove` calls). | ≤ 5 s (auto-expires; held only for the duration of a single move) |
| `playme:rate:{policy}:{key}` | Rate-limit counters (e.g. `playme:rate:create-room:{ip}`, `playme:rate:submit-move:{connectionId}`). | matches the rate-limit window |
| `playme:timeouts` | Sorted set of scheduled clock-timeout checks. Score = unix-ms deadline, value = `roomCode`. Swept by a `BackgroundService` (see §2.9 Clock model). | entries are removed by the sweeper after firing; stale entries expire when the room is `Closed`/`Expired` and the sweeper drops them |
| `playme:signalr:*` | SignalR backplane channels managed by `Microsoft.AspNetCore.SignalR.StackExchangeRedis`. **Don't read or write these manually.** | managed by the library |

Implementation rules:

- **State shape — single JSON blob per room, not decomposed.** The room key holds the entire `Room` aggregate (players, status, current match, clock fields, scoreboard, last-tick timestamp) serialized as one JSON document by `System.Text.Json`. **Do not split** into a Redis hash, multiple keys, or sub-documents. Rationale: writes are bounded by move rate (the clock model in §2.9 is lazy — state is only mutated on real events, not periodically), the document is small (~1–5 KB), one `GET` returns a consistent snapshot, and the C# `Room` aggregate maps 1:1 to the document so Infrastructure doesn't dictate Domain shape. **Exception:** append-only move history (for future replay support) lives in a separate `playme:room:{code}:moves` Redis list — additive, doesn't affect the main state schema.
- **Atomic move processing — Redis distributed lock per room.** Acquire `playme:room:{roomCode}:lock` via StackExchange.Redis `IDatabase.LockTakeAsync` (5 s TTL, library-generated unique token). Inside the lock: read room state → validate the move with the C# rules engine in `Domain` → write new state → call `LockReleaseAsync` (its release uses a small CAS Lua internally, so a lock is only released by the holder). Bound the acquire wait to ~500 ms; on timeout, reject the move with an `ErrorCode.Busy`-style code and let the client retry. **No application-level Lua scripts. No `WATCH/MULTI/EXEC` retry loops.** Rules logic stays in `Domain`/`Application` where it's testable and unique; the lock provides cross-instance mutual exclusion without duplicating it elsewhere. Contention is bounded by turn-based play (only the active player can legitimately move), so the lock is virtually uncontended in practice.
- **In-process C# locks are insufficient.** The API runs multi-instance behind a load balancer; `lock(obj)` only coordinates within one process. Any per-room critical section must use the Redis-distributed lock above. This earlier "don't use application-level locks" advice was specifically about in-process locks — Redis-distributed locks are the correct primitive.
- **TTL refresh on activity.** Every `Application` handler that reads or writes the room must refresh its TTL at the end (`EXPIRE` or `SET ... EX`). Idle rooms expire on their own.
- **Never store secrets, session tokens, or display names beyond the room state.** Cleanup happens via TTL, not by application code.
- **Don't enumerate keys by pattern (`KEYS playme:*`).** It's O(N) over the whole keyspace and blocks Redis. If enumeration becomes necessary, add a secondary index set (`playme:rooms:active`).

### 2.9 Room lifecycle / state machine

Every room follows a small finite-state machine, enforced server-side. Handlers reject transitions not in this table.

```
            ┌──────────────────────┐    TTL elapsed     ┌─────────────┐
            │  WaitingForOpponent  │───────────────────▶│   Expired   │  (terminal)
            └──────────┬───────────┘                    └─────────────┘
   both registered     │
   + both connected    │
            ┌──────────▼───────────┐
            │      InProgress      │
            └──────────┬───────────┘
                       │ win / draw / resign / timeout
            ┌──────────▼───────────┐
            │        Ended         │
            └──────────┬───────────┘
        rematch offered │       │ either player exits
            ┌──────────▼─────┐   │
            │ AwaitingRematch│   │
            └─┬──────────┬───┘   │
       accept │          │ reject │
              │          ▼        ▼
              │      ┌─────────────────┐
              │      │     Closed      │  (terminal)
              │      └─────────────────┘
              └──→ loops back to InProgress for a new Match
```

**States:**

- **`WaitingForOpponent`** — the initial state after room creation. **Governed purely by the room TTL** (default 30 min from creation, refreshed on challenger registration). Player disconnects in this state are **transparent**: host or challenger may close their tab and return at any time before the TTL elapses — the session cookie ties them to their role. Challenger registration (completing the join-onboarding form per §2.5) consumes the invite link; the seat is sticky and no one else can take it. **Transition to `InProgress` requires both: (1) both players have completed registration, AND (2) both currently have an active SignalR connection in the room.** Until both conditions hold, the room stays in `WaitingForOpponent`. If the TTL elapses before that, the room goes terminal to `Expired`.
- **`InProgress`** — both players joined; a match is being played. Clocks tick. The first-mover's clock starts immediately on entry (platform rule §2.3 #12).
- **`Ended`** — the current match has concluded. Post-match UI is shown. Either player can offer a rematch or exit.
- **`AwaitingRematch`** — one player offered a rematch; waiting for the opponent's accept/reject.
- **`Closed`** *(terminal)* — the room is cleaned up; both invite links are dead. Reached when (a) anyone exits without a rematch, (b) a rematch is rejected, (c) the post-`Ended` cleanup TTL elapses.
- **`Expired`** *(terminal)* — reached only from `WaitingForOpponent` when nobody joined within the TTL.

**Clock model (server-authoritative, lazy state).** The clock state lives in the Redis room hash: `lastTickAt` (server UTC ms), `activePlayer` (`host` | `challenger`), `hostClockMs`, `challengerClockMs`. Stored values represent remaining time *as of `lastTickAt`*. **No background timer mutates clock state every second.** The effective clock at moment `T` is computed lazily — for the active player: `storedClockMs - (T - lastTickAt)`; for the inactive player: the stored value unchanged. State is rewritten only when something changes (move accepted, match ends, room closes).

Clients extrapolate locally between server snapshots (`displayedMs = serverClockAtSnapshot - (Date.now() - snapshotReceivedAt)`) and re-sync whenever a `ClockTick` arrives. Drift is bounded by network RTT and irrelevant at 1-second display granularity.

**Timeout detection** is two-pronged:

1. *At move time* — the move handler recomputes the active player's effective clock *before* validating the move. If it's `≤ 0`, emit `MatchEnded(Outcome.Timeout)` instead of accepting the move.
2. *No-move timeout* — when a room enters `InProgress` or accepts a move, schedule one delayed timeout check at `lastTickAt + activePlayerRemainingMs`. Implementation: a Redis sorted set (`playme:timeouts`, score = unix-ms deadline, value = `roomCode`), swept by a single `BackgroundService` per API instance every ~250 ms via `ZRANGEBYSCORE ... LIMIT 0 N`. **The sweeper MUST acquire the room lock (`playme:room:{roomCode}:lock` per §2.8) before processing each expired entry** — this serializes timeout processing against concurrent move handlers and against other API instances' sweepers, preventing duplicate `MatchEnded` emissions. The processing sequence for each expired entry: (1) attempt `LockTakeAsync` with a short acquire wait (~100 ms); on lock-contention, skip — the next sweep will retry. (2) Inside the lock, re-read the room state. (3) If the room is still `InProgress`, `lastTickAt` hasn't advanced, the active player is unchanged, and effective clock is ≤ 0, emit `MatchEnded(Outcome.Timeout)` and transition the room to `Ended`. Otherwise drop silently (a move happened, a new check was scheduled). (4) `ZREM` the entry regardless of outcome — the entry has been adjudicated. (5) Release the lock. Dead-instance safety: if the sweeper crashes mid-processing, the lock's 5 s TTL releases it automatically; the `ZREM` never happens, so the entry stays in the set and the next sweep retries.

This pattern means **one scheduled task per active room — not a per-room 1-second timer.** It matches the approach Lichess and Chess.com use for authoritative chess clocks at scale.

**Server-emitted events** (broadcast to both clients via SignalR unless noted):

| Event | When | Payload |
|---|---|---|
| `OpponentJoined` | challenger join | challenger display name, side/color |
| `MatchStarted` | entering `InProgress` (initial or after rematch accept) | starting clock snapshot, both players' sides (**swapped on each rematch** per §2.3 #15), who moves first |
| `MoveAccepted` | server accepts a `SubmitMove` | move, updated board state, who's next, clock snapshot |
| `MoveRejected` | server rejects a `SubmitMove` | reason code (illegal cell, full column, not-your-turn) — sent to submitter only |
| `ClockTick` | on every accepted move, on connect/reconnect, on match end, and (optional) a slow drift-correction sweep every 5–10 s | per-player remaining time |
| `MatchEnded` | `InProgress` → `Ended` | outcome, winning-line coordinates (if `Win`), final clock |
| `RematchOffered` | a player offers rematch | which player offered |
| `RematchAccepted` | `AwaitingRematch` → `InProgress` (new match) | (followed by `MatchStarted`) |
| `RematchDeclined` | `AwaitingRematch` → `Closed` | sent to the offerer; the rejector is auto-routed to the lobby |
| `OpponentDisconnected` | a player's SignalR connection drops | sent to the still-connected player |
| `OpponentAbandoned` | reconnect grace (§2.3 #7, default 30 s) elapses without reconnect | sent to the still-connected player; unlocks `ClaimVictory()` on their UI |
| `OpponentReconnected` | dropped player rejoins (before *or after* grace, while match is still `InProgress`) | sent to the still-connected player |
| `OpponentExited` | a player leaves the room while in `Ended` or `AwaitingRematch` (explicit `ExitRoom()` call or tab-close disconnect — both treated identically) | sent to the still-present player; their UI shows "opponent left" + a manual "Back to lobby" button. Room transitions to `Closed`. |
| `RoomExpired` | room reaches `Expired` or post-`Ended` cleanup TTL | reason |

**Invariants:**

- All transitions are server-driven. Clients can *request* transitions (`SubmitMove`, `OfferRematch`) but cannot *perform* them.
- A move is only accepted in `InProgress`.
- A rematch offer is only accepted in `Ended` and transitions to `AwaitingRematch`.
- The clock keeps running during `OpponentDisconnected` (platform rule §2.3 #7).
- `ClaimVictory()` is valid only when (a) the room is `InProgress`, (b) the opponent is currently disconnected, and (c) the §2.3 #7 grace has expired (i.e. `OpponentAbandoned` has been emitted). It transitions the match to `Ended` with `Outcome.Disconnect(opponent)`.
- `ExitRoom()` is valid only in `Ended` or `AwaitingRematch`. It transitions the room directly to `Closed` and emits `OpponentExited` to the still-present player. **A tab-close / SignalR disconnect from `Ended` or `AwaitingRematch` is treated identically** — same transition, same event. There is no post-match reconnect grace; the clock isn't running, so there's no fairness reason to wait. (The §2.3 #7 grace applies only to `InProgress`.) Once `Closed`, the room is non-joinable, all subsequent Hub calls return `ErrorCode.RoomClosed`, and the post-`Ended` TTL (5 min per §2.8) eventually deletes the Redis state.

---

## 3. Localization (i18n)

- Two locales at launch: **Georgian (`ka`)** and **English (`en`)**. `ka` is the default; `en` is the fallback.
- Web uses **`i18next` + `react-i18next`**. Catalogs are loaded from `packages/shared/i18n/<locale>.json`.
- **Never hard-code user-facing text.** Every visible string must go through a translation key. This includes error messages, button labels, toast text, meta tags, OG titles, and PWA manifest names.
- Backend returns **localized error codes** (e.g. `errors.room.expired`), not localized strings. The client maps codes to translations.
- When introducing new UI text, add the key to **both** `ka.json` and `en.json` in the same PR. Missing translations should fall back to `en`, not show a raw key.

### Error code naming convention

Backend produces **error codes**; clients map them to localized messages. Two mirroring concepts:

- **`ErrorCode` enum** in C# (`PlayMe.Application/Errors/ErrorCode.cs`) — `PascalCase` values: `RoomClosed`, `RoomExpired`, `InvalidSide`, `Busy`, etc.
- **i18n keys** in `packages/shared/i18n/{ka,en}.json` — dot-separated, lowercase `camelCase`: `errors.room.closed`, `errors.room.expired`, `errors.join.invalidSide`, `errors.room.busy`.

The mapping `ErrorCode.<EnumValue>` ↔ `errors.<category>.<camelCase>` is deterministic and lives in one place (an attribute on the enum, or a small mapping table). Every enum value has a corresponding i18n key in **both** locales — missing translations fall back to `en`, not a raw code.

**Categories in use** (extend as needed; add new categories to this list when introducing them):

| Category | Example codes | Domain |
|---|---|---|
| `errors.validation.*` | `displayName`, `move` | input validation (FluentValidation / Zod) |
| `errors.config.*` | `invalidTimeLimit`, `invalidGameId` | room-creation configuration |
| `errors.join.*` | `sideNotAllowed`, `sidePickRequired`, `invalidSide` | challenger join flow |
| `errors.room.*` | `notFound`, `expired`, `closed`, `alreadyJoined`, `busy`, `notJoinable` | room state errors |
| `errors.move.*` | `illegalCell`, `fullColumn`, `notYourTurn` | move-time validation |
| `errors.rematch.*` | `illegalTransition` | rematch flow |
| `errors.session.*` | `invalid`, `expired`, `unauthorized` | session / authentication |

**This is a naming convention, not a complete catalog.** The exhaustive list of codes lives in the C# enum + the i18n JSON files. New codes are added to both the enum and **both locale catalogs** in the same PR.

---

## 4. Observability

### 4.1 Errors — Sentry

- Frontend (`apps/web`) wires Sentry via `@sentry/nextjs`. Source maps uploaded on deploy.
- Backend (`apps/api`) wires Sentry via `Sentry.AspNetCore`. Releases tagged with the deployed commit SHA.
- Sentry retention is platform-controlled (Sentry free tier ≈ 30 days). Don't try to configure it from code.

### 4.2 Product analytics — PostHog

- Anonymous, cookie-based event tracking. No PII.
- Baseline event set to instrument from day one:
  - `room_created`, `room_joined`, `room_expired`
  - `match_started`, `move_made`, `match_ended` (with `reason`: `win` | `draw` | `resign` | `timeout` | `disconnect`)
  - `rematch_offered`, `rematch_accepted`, `rematch_rejected`
- Events fire from the **web client** for user-facing actions and from the **API** for authoritative outcomes (match end, room expiry). The API uses PostHog's .NET SDK; events tagged `source: server` vs `source: web`.

### 4.3 Logging — Serilog

- API uses **Serilog** with structured logging.
- **7-day rolling file sink** (`logs/playme-.log`, `rollingInterval: Day`, `retainedFileCountLimit: 7`). Older files are pruned automatically.
- Always inject `ILogger<T>`; use structured templates, never string interpolation:

```csharp
_logger.LogInformation("Move accepted in room {RoomId} by {PlayerRole}", roomId, role);
```

- **Never log secrets, invite tokens, or display names** at `Information` level or above. Display names go in PostHog events (anonymous) but not in error logs.

### 4.4 Tracing — OpenTelemetry

- API emits OTel traces and metrics via the `OpenTelemetry.Extensions.Hosting` packages.
- v1 ships traces to **stdout / file** only. A managed backend (Grafana Cloud, Honeycomb, etc.) is **deferred** until we scale beyond one API instance.
- Application Insights is **not** used in v1 (Sentry + PostHog cover the same ground at $0).

---

## 5. Security

Security is a first-class concern. The threat surface is small (anonymous, ephemeral, no PII storage) but the rules below are what makes "server-authoritative everything" actually safe in practice.

### 5.1 Threat model

The realistic threats for PlayMe:

- **Cheating / client-side rule violations** — a client sends an illegal move, claims a win, or tries to manipulate its own clock. Mitigation: server is the only authority for state, clock, and rules.
- **Room hijacking** — someone guesses or scrapes an invite link, or one player tries to act as the other. Mitigation: unguessable room codes + per-role session tokens + server-side authorization on every action.
- **Abuse** — display-name attacks, room-creation spam, move flood. Mitigation: input validation + rate limiting + connection caps.
- **Leaked secrets** — Sentry/PostHog/Redis credentials end up in logs, source maps, or the public bundle. Mitigation: secrets stay server-side; logging never includes them.
- **Cross-site attacks** — XSS via display names, CSRF on state-changing requests, clickjacking on invite links. Mitigation: React's default escaping, security headers, SameSite cookies, CSP.

Persistent-data risk is low (no accounts, no DB), but rate-limit and credential hygiene still matter.

### 5.2 Transport & secrets

- **HTTPS / WSS only.** HSTS preload on `playme.ge`. TLS between API and Redis (`Azure Cache for Redis` configured TLS-only).
- **Secrets stay server-side.** Sentry server DSN, PostHog personal API key, Redis connection string, signing keys — env vars in Azure App Service / Vercel only. **Only** the public-tier client keys (Sentry public DSN, PostHog public project key) may ship in the web bundle. Nothing else.
- **No secrets in repo, ever.** `.env*` is gitignored. Local dev uses `.env.local` (web) and .NET user-secrets (API). CI uses repo secrets / OIDC; never echo secrets in build logs.

### 5.3 Input validation

- **Web:** every external input — URL params, form fields, query strings, server-pushed SignalR messages — is parsed through **Zod** before touching app state. Schemas live in `packages/shared`.
- **API:** every controller action and SignalR Hub method validates its DTO with **FluentValidation** before reaching a handler. No `[FromBody]` payload reaches `Application` unchecked.
- **Display names:** max 24 chars; allow Unicode letters, digits, spaces, and a small punctuation allowlist; strip control characters, zero-width characters, and RTL/LTR-override codepoints before storing or echoing.
- **No `dangerouslySetInnerHTML`** without an explicit sanitizer review. React's default escaping is the default and the rule.

### 5.4 Room & player identity

- **Room codes** are opaque, high-entropy tokens (≥128 bits) generated with a **cryptographic RNG**. Never sequential, never derived from time alone, **never `Guid.NewGuid()`** (it isn't cryptographically random).
- **Player session tokens.** On room creation, the API issues a signed, short-lived token tied to one player, one room, one role (`host` or `challenger`). **Delivered as an HttpOnly cookie — no bearer-token path in v1.** Cookie attributes: `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/`, `Domain=playme.ge` (with the equivalent for dev/staging origins). Lifespan ≤ 6 hours, refreshed on activity, invalidated when the room is cleaned up. Payload (signed via ASP.NET Core's Data Protection API or a JWT — either is fine, the API decides) carries `roomCode`, `playerId` (see §2.7), `role`, and `exp`. The token is opaque to the client. The SignalR JS client connects with `withCredentials: true` so the cookie is sent on both the HTTP negotiate request and the WebSocket upgrade — **do not** use `accessTokenFactory`. Mobile (React Native, v2) will additionally support bearer tokens at that time; v1 is cookie-only, one mechanism, one code path.
- **`playerId` generation and storage.** Generated by the API using cryptographic RNG (`RandomNumberGenerator.Fill`, 128 bits, URL-safe base64 — same primitive as `RoomCode`) at exactly two moments: on `POST /api/rooms` (host's `playerId` → stored as `room.hostPlayerId`) and on `POST /api/rooms/{code}/join` (challenger's `playerId` → stored as `room.challengerPlayerId`). The same value is embedded in the issued session cookie. `playerId` is not reused across rooms and not persisted beyond the room's lifetime.
- **Authorization on every action.** Every Hub method and controller action runs the following check before doing anything else: (1) parse and validate the signed session cookie; (2) load the room by `cookie.roomCode`; (3) look up the stored `playerId` for `cookie.role` (`room.hostPlayerId` or `room.challengerPlayerId`); (4) **reject if the stored `playerId` does not match `cookie.playerId`** (stale/forged/replayed token); (5) verify the action is allowed for that role in the current room state (e.g. only the player whose turn it is can submit a move; only the connected player can `ClaimVictory()` after `OpponentAbandoned`). The client never claims a role or playerId — the server decides from the token.

Reference implementation for room codes (C#):

```csharp
public static string NewRoomCode()
{
    Span<byte> bytes = stackalloc byte[16]; // 128 bits
    RandomNumberGenerator.Fill(bytes);
    return WebEncoders.Base64UrlEncode(bytes); // URL-safe, ~22 chars
}
```

### 5.5 Rate limiting & abuse

Rate limits operate at three distinct scopes, each with a different lifetime. Pick the right one based on whether a session exists and whether the limit must survive a SignalR reconnect.

- **Per IP** — pre-session HTTP requests where no session cookie has been issued yet. Used for room creation (`POST /api/rooms`) and the challenger's join request. Also used at the Vercel edge, which doesn't see session cookies.
- **Per session** — actions taken by an authenticated player using the session cookie from §5.4. **Survives SignalR reconnects**, since one session can spawn many connections over its ≤ 6 h lifetime. Use for any per-action quota that should persist across reconnects (move flood, rematch spam, resign spam).
- **Per connection** — a single SignalR WebSocket's lifetime. **Resets on every reconnect**, so it's only useful as a **burst ceiling** ("≤ N messages/sec from this socket") to catch a runaway client — never as a per-action quota (an abuser would just reconnect to refill).

**Rule of thumb:** if the limit must survive a tab refresh or network blip, it's per-session. If it's "this socket is flooding right now," it's per-connection. If no session exists yet, it's per-IP.

**Concrete policies (initial starting points — tune from real traffic):**

| Action | Layer | Scope | Limit |
|---|---|---|---|
| `POST /api/rooms` | ASP.NET rate-limit middleware | per IP | 10 / min |
| `POST /api/rooms/{code}/join` | ASP.NET rate-limit middleware | per IP + per room code | 5 / min per IP, 10 / hr per code |
| `RoomHub.SubmitMove` (sustained) | Application-layer check | per session | 60 / min |
| `RoomHub.SubmitMove` (burst) | SignalR pipeline | per connection | 10 / sec |
| `RoomHub.OfferRematch` / `Accept` / `Reject` | Application | per session | 30 / min |
| `RoomHub.Resign` | Application | per session | 5 / min |
| `RoomHub.ClaimVictory` | Application | per session | 5 / min |
| `RoomHub.ExitRoom` | Application | per session | 10 / min |
| Any SignalR message | SignalR pipeline | per connection | 20 / sec hard ceiling |
| `/r/<roomCode>` enumeration | Vercel edge | per IP | 60 / min |

**Implementation:**

- **Per-IP** and **per-IP + per-room** policies use ASP.NET Core's built-in `RateLimiter` middleware with the appropriate partition key.
- **Per-session** policies are enforced in the `Application` layer via an `IRateLimiter` port (interface in `PlayMe.Application/Abstractions/`, implementation in `PlayMe.Infrastructure/`). The implementation uses a Redis sliding window keyed by `playme:rate:{policy}:session:{sessionId}` (matches the §2.8 schema).
- **Per-connection** burst ceilings use SignalR's `HubOptions` plus a per-message hub filter for the hard ceiling.

**Observability hook:** whenever a rate limit fires, log a structured event (`RateLimitExceeded { policy, scope, key }`) at `Warning`, emit a PostHog event tagged `source: server`, and bump a counter. Recurring abuse signals should trip Sentry breadcrumbs and (post-launch) an on-call alert per §5.11.

### 5.6 HTTP security headers

Configured in Next.js (`next.config.js` `headers()`) for the web and in ASP.NET Core middleware for the API.

- **Content-Security-Policy** — strict. `default-src 'self'`; explicit allowlist for Vercel, Sentry, PostHog. No inline scripts (use `next/script` with nonces if absolutely required).
- `Strict-Transport-Security: max-age=63072000; includeSubDomains; preload`.
- `X-Frame-Options: DENY` (clickjacking matters because invite links are highly share-friendly).
- `X-Content-Type-Options: nosniff`.
- `Referrer-Policy: strict-origin-when-cross-origin`.
- `Permissions-Policy` — deny camera, microphone, geolocation, USB, payment, accelerometer, magnetometer.

Audit headers with `securityheaders.com` before each release; target **A+**.

### 5.7 CORS

- API CORS allowlist: `https://playme.ge` (prod) plus explicitly named dev/staging origins. **Never `*`** — not even temporarily.
- SignalR CORS uses the same allowlist with credentials enabled (cookies must be sent).

### 5.8 Privacy & logging

- **Never log** session tokens, invite codes, Redis connection strings, or display names at `Information` level or higher (reinforces §4.3).
- IP addresses are PII in the EU. Sentry: `send_default_pii: false`. PostHog: disable IP capture, disable autocapture, only emit the explicit events listed in §4.2.
- Cookie banner is required only when a cookie is set that needs consent. PostHog can run cookieless; prefer that for v1 to avoid the banner entirely.

### 5.9 Dependency security

- **Renovate** (or Dependabot) configured for both `pnpm` and NuGet. PRs reviewed at least weekly.
- CI runs `pnpm audit --prod` and `dotnet list package --vulnerable --include-transitive`; **high or critical findings fail the build**.
- Pin major versions. Minor/patch bumps may auto-merge for non-prod-runtime packages once tests are green; everything else requires a human approval.

### 5.10 Static analysis

- `eslint-plugin-security` enabled in the web workspace.
- **CodeQL** (GitHub-native, free) wired into CI for both JS/TS and C# branches. Findings labeled `error` block merge.

### 5.11 Incident response

- Sentry alerts route to the on-call channel (channel TBD — see §11).
- If a secret leaks: rotate the Redis password, regenerate Sentry/PostHog keys, invalidate any active session tokens, redeploy. Azure Cache for Redis supports password rotation without downtime.
- Post-incident: write a short note in the repo (`docs/incidents/<date>-<slug>.md`) so we don't repeat it.

---

## 6. Deployment

| Component | Platform | Tier (MVP) |
|---|---|---|
| `apps/web` (Next.js) | **Vercel** | Free tier |
| `apps/api` (ASP.NET Core) | **Azure App Service for Linux** | B1 (~$13/mo) |
| Redis (state + SignalR backplane) | **Azure Cache for Redis** | Basic C0 (~$15/mo) |
| Sentry | Sentry Cloud | Free tier |
| PostHog | PostHog Cloud | Free tier |

- The API ships with a **Dockerfile from day one** even though App Service for Linux can run code directly. This keeps local-dev / cloud parity and makes a future move to Azure Container Apps (or any container platform) a config change, not a re-architecture.
- **SignalR uses a Redis backplane** (via `Microsoft.AspNetCore.SignalR.StackExchangeRedis`). No Azure SignalR Service. The Redis instance does double duty as state store and pub/sub fan-out.
- All secrets via environment variables in App Service / Vercel. **Never** commit `.env` files. Local dev uses `appsettings.Development.json` + .NET user-secrets for the API, and `.env.local` (gitignored) for the web app.

---

## 7. Build / Test / Run Commands

The repo uses **pnpm** + **Turborepo** for JS/TS, and the **dotnet CLI** for the API.

### Install

```bash
pnpm install                       # installs all JS workspaces
dotnet restore apps/api            # restores .NET dependencies
```

### Run (dev)

```bash
pnpm dev                           # turbo: starts web + watch tasks
pnpm --filter web dev              # web only (Next.js dev server)
dotnet run --project apps/api      # API on https://localhost:5001
docker compose -f infra/docker-compose.yml up redis   # local Redis
```

### Build

```bash
pnpm build                         # turbo: builds all JS workspaces
pnpm --filter web build            # production Next.js build
dotnet build apps/api -c Release
docker build -f infra/api.Dockerfile -t playme-api .  # container image
```

### Test

```bash
pnpm test                          # turbo: runs all JS test tasks (none yet — add as you go)
dotnet test                        # runs .NET tests (none yet — add as you go)
```

Preferred test frameworks when introducing the first tests:

- **Web:** Vitest (unit) + Playwright (e2e).
- **API:** xUnit + FluentAssertions + WebApplicationFactory for integration tests.

### Lint / format / typecheck

```bash
pnpm lint                          # eslint across workspaces
pnpm format                        # prettier --write
pnpm typecheck                     # tsc --noEmit across workspaces
dotnet format apps/api             # .NET formatter (respects .editorconfig)
```

### Codegen

```bash
pnpm gen:api                       # regenerates the TS API client from the API's OpenAPI doc
```

**Always run** `pnpm typecheck` and `pnpm lint` before declaring a frontend change "done". For backend changes, run `dotnet build` and `dotnet format --verify-no-changes`.

---

## 8. Code Style & Conventions

### TypeScript (`apps/web`, `packages/shared`)

- `strict: true` in every `tsconfig.json`. **No `any`** — use `unknown` and narrow.
- Prefer **named exports**. Default exports only where a framework requires them (Next.js page/layout components, route handlers).
- Async over `.then()`. No floating promises — every promise is awaited or explicitly `void`-ed with a comment.
- Validate all external input (HTTP responses, route params, env vars, SignalR messages) with **Zod**. Schemas live in `packages/shared`.
- React: function components only. Hooks at top level. Co-locate component + styles + tests in the same folder.
- State: prefer local state + URL state. Reach for a store (Zustand) only when state is genuinely cross-feature.
- Imports: use path aliases (`@/features/...`, `@shared/...`); no deep relative imports (`../../../`).

### C# / .NET (`apps/api`)

- Target **.NET 10 (LTS)**. Pin via `global.json`. Nullable reference types **on** project-wide.
- **File-scoped namespaces**. One public type per file.
- Async all the way: every I/O method is `async Task<...>`; no `.Result` / `.Wait()`. Pass `CancellationToken` through.
- DTOs are `record` types. Domain entities are classes with private setters and behavior, not anemic bags.
- Use `IOptions<T>` for configuration; never read `IConfiguration` directly inside business code.
- Errors: throw domain exceptions for invariants; return `Result<T>` (or `ProblemDetails`) for expected failure paths. **No exceptions for control flow.**
- Logging: inject `ILogger<T>`; structured templates only.
- Naming: `PascalCase` for public, `_camelCase` for private fields, `camelCase` for locals. Interfaces prefixed with `I`.
- Redis access goes through a typed repository in `Infrastructure/`. Hubs and controllers never touch `IConnectionMultiplexer` directly.

### Design principles (SOLID)

SOLID is the design baseline for **both** the backend and the frontend. The rules below are the operational reading — what to do in this codebase, not abstract definitions.

**S — Single Responsibility.** Each unit has one reason to change.

- *Backend:* one handler per use case (`CreateRoomHandler`, `SubmitMoveHandler`). Repositories own a single aggregate. Controllers/hubs do I/O translation only — no rules, no orchestration of multiple handlers.
- *Frontend:* a React component renders one thing; a hook owns one piece of state or one effect; a service module talks to one concern. If a component is doing fetching + state + layout + business rules, split it.

**O — Open/Closed.** Open for extension, closed for modification.

- *Backend:* adding a new game module must **not** require editing existing modules or the platform layer. Each game is a self-contained extension; the platform exposes hooks (move pipeline, clock tick, room lifecycle) that game modules plug into. If you find yourself editing a platform-layer `switch` statement to add a game, redesign the seam.
- *Frontend:* prefer composition + props + render-props/children over modifying a shared component to add a special case. New variants come from new components or strategies, not from `if (game === 'connect4')` branches inside a generic component.

**L — Liskov Substitution.** A subtype must honor its base contract.

- *Backend:* every implementation of an `Application` port (e.g. `IRoomRepository`) must behave per the interface's documented contract — same exceptions, same null/empty semantics, same idempotency guarantees. No `throw new NotImplementedException()` in a registered implementation.
- *Frontend:* a polymorphic component's variants must respect the same prop contract; don't silently ignore a prop in one variant.

**I — Interface Segregation.** Many narrow interfaces beat one fat one.

- *Backend:* split ports by responsibility — `IRoomRepository`, `IClockService`, `IMoveValidator`, `IAnalytics` — instead of one `IGameService` with twenty methods. Handlers depend only on the ports they need, which keeps unit tests small.
- *Frontend:* component props and hook signatures expose only what the caller needs. Don't pass the whole `Match` object when only `match.id` is used; the wider the surface, the harder the refactor.

**D — Dependency Inversion.** Depend on abstractions, not concretions.

- *Backend:* this is the spine of our Clean Architecture. `Application` defines ports; `Infrastructure` implements them; `Domain` and `Application` never reference concrete adapters or third-party SDKs. Composition happens in `Api/DependencyInjection/`. **Never `new` an infrastructure concern inside a handler.**
- *Frontend:* features depend on the generated API client and typed hooks — not on raw `fetch`, not on direct SignalR `HubConnection` instances. Wrap externals in modules that present a stable interface, so swapping the transport or the analytics SDK is a one-file change.

When a SOLID violation is necessary (pragmatism over purity), call it out in the PR description with a one-line rationale and, where possible, a follow-up ticket.

### Shared rules

- Every file ends with a newline. No trailing whitespace.
- Commit-ready code: no commented-out blocks, no `TODO` without a ticket reference (`// TODO(#123): ...`).
- Public APIs (exported TS, public C#) must have doc comments explaining intent, not just restating the signature.
- **No hard-coded user-facing strings** — always through an i18n key.

---

## 9. Workflow Rules

### Branching

- **Trunk-based.** `main` is always releasable.
- Branch names: `feat/<short-slug>`, `fix/<short-slug>`, `chore/<short-slug>`. Keep them short-lived (< 3 days when possible).
- Never commit directly to `main`. Every change goes through a PR.

### Commits

- **Conventional Commits**: `feat:`, `fix:`, `chore:`, `refactor:`, `docs:`, `test:`, `perf:`.
- Subject in imperative mood, ≤ 72 chars. Body explains *why*, not *what*.
- Squash-merge PRs into `main` so history stays linear.

### Pull requests

- One logical change per PR. If a PR grows past ~400 changed lines, split it.
- PR description must include: what changed, why, how it was tested, screenshots/recordings for UI changes, and any follow-ups left behind.
- CI must be green. Required checks: lint, typecheck, `dotnet build`, `dotnet format --verify-no-changes`, and tests (once they exist).
- At least one review approval before merge.

### What Claude Code should do automatically

- Run `pnpm typecheck` and `pnpm lint` after non-trivial TS edits.
- Run `dotnet build` after non-trivial C# edits.
- Update or add Zod schemas in `packages/shared` when API contracts change, and regenerate the TS API client (`pnpm gen:api`).
- Add or update i18n keys in **both** `ka.json` and `en.json` when introducing new UI text.
- Treat the **server as the source of truth** for game state and the clock. Never add client-side game logic that could disagree with the server.
- When a change touches the platform layer (rooms, clock, reconnect, rematch), verify every game module still behaves correctly.
- When adding a new backend feature, put the logic in an `Application/` **handler** (Command or Query) and call it from a thin controller or hub. Don't grow controllers/hubs with business logic.
- When wrapping a new external service (any SDK, any HTTP client), **define the interface in `Application/Abstractions/` first, implement it in `Infrastructure/`**, and register it in DI. Never let `Application` or `Domain` import a third-party SDK directly.
- Apply **SOLID** on every change (backend and frontend). Before merging, mentally walk the five principles: is this unit single-purpose, extensible without modification, contract-honoring, narrowly typed, and depending on abstractions? If a principle is being violated on purpose, note it in the PR description.
- When adding or changing a public page (anything not under `/r/`), include the full SEO surface: `metadata` (title, description, canonical, OG, Twitter, robots), `hreflang` alternates for both locales, and a sitemap entry. Treat a page without metadata as incomplete.
- Update `sitemap.ts` whenever a new public, indexable route is added or removed.
- **Validate every external input** on both sides: Zod on the web (URL params, forms, server-pushed messages) and FluentValidation on the API (every controller / Hub method DTO). No exceptions.
- **Authorize every Hub method and controller action** by session token + room role. The server decides what a caller may do; the client never claims its role.
- **Use a cryptographic RNG** for any token, room code, or signed value (`RandomNumberGenerator` in C#, `crypto.getRandomValues` on the web). Never `Math.random()`, `Guid.NewGuid()`, or anything derived from `DateTime`.
- After security-relevant changes (auth, CORS, headers, rate limiting, dependency bumps), run `pnpm audit --prod` + `dotnet list package --vulnerable --include-transitive` and re-check headers via `securityheaders.com`.
- When unsure about a tradeoff that affects the platform layer or more than one game, **stop and ask** rather than guessing.

### What Claude Code should NOT do

- Don't introduce a shared rules engine across game modules. Each game is intentionally self-contained.
- Don't switch to minimal APIs. The API uses full controllers (`ControllerBase` + `[ApiController]`).
- Don't ship a new public page without metadata, canonical URL, hreflang alternates, and a sitemap entry. SEO is a hard requirement, not a polish item.
- Don't make `/r/<roomId>` (rooms) indexable. Room pages are private/ephemeral — `noindex, nofollow` always.
- Don't use sequential or guessable room IDs in URLs. Room codes are opaque tokens.
- Don't render public pages client-only. Public pages are SSR or SSG; only the in-match UI is CSR.
- Don't load custom fonts or images without `next/font` / `next/image` — Core Web Vitals (LCP, CLS) are part of the SEO contract.
- Don't hard-code colors (hex / rgb / Tailwind color literals like `bg-blue-500`) in components. Always go through a semantic theme token so both light and dark modes work. New visuals (board, pieces, win highlight) must be defined as tokens, not raw colors.
- Don't ship a UI change without verifying it in **both** light and dark mode, and with the OS set to each preference.
- Don't use `Math.random()`, `Guid.NewGuid()`, or `DateTime`-derived values for security tokens, room codes, or anything that needs to be unguessable. Use a cryptographic RNG.
- Don't render arbitrary user input through `dangerouslySetInnerHTML`. No exceptions for "trusted" inputs — user-controlled is user-controlled.
- Don't log session tokens, invite codes, Redis URIs, or other secrets at any level. Structured templates make it easy to accidentally leak — review log statements that include request bodies or DTOs.
- Don't widen CORS to `*` or weaken CSP "temporarily" to ship a feature. Add the specific origin / source explicitly.
- Don't add a new third-party JS dependency to the web bundle without considering CSP impact, supply-chain risk, and what it loads at runtime.
- Don't ship a new public route without the standard security headers attached.
- Don't trust a client-claimed role, player id, or game-state field. The server is the only authority — period.
- Don't violate the Clean Architecture dependency rule. `Domain` references nothing; `Application` references only `Domain`; `Infrastructure` and `Api` reference inward. Project references are the source of truth.
- Don't expose `Domain` entities through controllers, hubs, or SignalR messages. Map to DTOs at the `Api` boundary.
- Don't put business logic, validation, or rules in controllers or hubs. They translate I/O; handlers in `Application/` decide.
- Don't call `DateTime.UtcNow` (or `DateTimeOffset.UtcNow`) inside `Domain` or `Application`. Inject `IClock` and use it.
- Don't add Azure SignalR Service, Application Insights, or other paid services without discussion — the v1 cost model is explicit.
- Don't bypass the generated API client by hand-rolling `fetch` calls in features.
- Don't add `any`, disable strict mode, or suppress lint rules to make code compile.
- Don't pause the chess clock on disconnect — the design is that the clock keeps running during reconnect grace.
- Don't add user-facing strings without translations in both locales.
- Don't introduce new top-level dependencies without flagging them in the PR description (license, size, maintenance).
- Don't change CI configuration, release scripts, `turbo.json`, or the deploy targets silently — call them out.
- Don't commit secrets, `.env` files, or local certificates. Use .NET user-secrets for the API and `.env.local` (gitignored) for the web app.

---

## 10. Quick Reference — Where Things Live

| Concern | Location |
|---|---|
| Shared TS types & Zod schemas | `packages/shared/src/` |
| Generated API client | `packages/shared/src/api/` (do not edit by hand) |
| SignalR client wrapper | `packages/shared/src/realtime/` |
| Translation catalogs | `packages/shared/i18n/{ka,en}.json` |
| Game rules catalog (shared, localized) | `packages/shared/i18n/rules/{ka,en}.json` |
| Web routes | `apps/web/app/` |
| Web features | `apps/web/features/<feature>/` |
| PWA manifest & icons | `apps/web/public/` |
| Theme tokens (light/dark CSS variables) | `apps/web/app/globals.css` |
| Theme provider / toggle | `apps/web/features/theme/` |
| Backend controllers | `apps/api/src/PlayMe.Api/Controllers/` |
| SignalR hub (single `RoomHub`) | `apps/api/src/PlayMe.Api/Hubs/RoomHub.cs` |
| DI composition root | `apps/api/src/PlayMe.Api/DependencyInjection/` |
| Use cases / handlers (Commands + Queries) | `apps/api/src/PlayMe.Application/` |
| Application ports (interfaces) | `apps/api/src/PlayMe.Application/Abstractions/` |
| Game modules (rules, state) | `apps/api/src/PlayMe.Domain/Games/<game>/` |
| Platform domain (room, clock) | `apps/api/src/PlayMe.Domain/Platform/` |
| Redis repositories (port impls) | `apps/api/src/PlayMe.Infrastructure/Redis/` |
| Sentry / OTel / Serilog wiring | `apps/api/src/PlayMe.Infrastructure/Telemetry/` |
| Security headers (web) | `apps/web/next.config.js` (`headers()`) |
| Security headers (API) | `apps/api/src/PlayMe.Api/Middleware/SecurityHeaders.cs` |
| CORS config | `apps/api/src/PlayMe.Api/DependencyInjection/AddApi.cs` |
| Rate-limiting policies | `apps/api/src/PlayMe.Api/RateLimiting/` |
| Token / room-code generation | `apps/api/src/PlayMe.Infrastructure/Security/` |
| FluentValidation validators | `apps/api/src/PlayMe.Application/Validation/` |
| Dockerfile (API) | `infra/api.Dockerfile` |
| Local Redis compose | `infra/docker-compose.yml` |
| OpenAPI → TS codegen | `tools/gen-api/` |
| Editor config | `/.editorconfig` (root) |
| ESLint / Prettier base | `packages/config/` |

---

## 11. Implementation roadmap

The recommended construction sequence. Each sprint should land an **end-to-end vertical slice** (deployable, demonstrable), not a horizontal layer. Sprint lengths are nominal — slip-or-cut scope, never quality. The first vertical slice is the highest-risk piece because it exercises every layer; don't add features until it ships.

**Sprint 0 — Bootstrap (~1 week).** A hello-world that exercises every piece of infrastructure.

- Initialize the monorepo (pnpm + Turborepo) per §2.2.
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
- Infrastructure: `RedisRoomRepository` using the **Redis distributed-lock pattern from §2.8** (`LockTakeAsync` / `LockReleaseAsync` on `playme:room:{code}:lock`, 5 s TTL, ~500 ms acquire budget). `RoomCodeGenerator` (cryptographic RNG per §5.4).
- API: `RoomsController` (POST `/api/rooms`, GET `/api/rooms/{code}`), `RoomHub` at `/hubs/room` (per §2.4) with `JoinRoom` and `SubmitMove` methods.
- Web: landing card grid (one card) **with the "How PlayMe works" section already included**, configure page **with a rules tab/panel**, room/match page with the board UI (including the **last-move highlight**) and a "share link" button.
- Generated API client wired (`pnpm gen:api`).
- Server-authoritative validation + win detection. `MatchEnded` includes the winning-line coordinates.

**Exit criteria:** Two browser tabs play a full game from a shared link; illegal moves are rejected with a clear error; the server is the rules authority.

**Sprint 2 — Chess clock + reconnect (~1 week).**

- `IClock` in `Application/Abstractions`; `SystemClock` in `Infrastructure`.
- Server-side clock ticking; `ClockTick` events at ~1s and on every accepted move.
- Match ends on timeout → `MatchEnded` with `Outcome.Timeout`.
- Client renders what `ClockTick` says — no client-side free-run.
- SignalR reconnect with a 30s grace window. Clock keeps running through disconnect. New events: `OpponentDisconnected`, `OpponentReconnected`.

**Exit criteria:** A game can time out; a player can close and reopen a tab within 30s without losing state.

**Sprint 3 — Connect 4 (~1 week).**

- New self-contained game module `connect4` (gravity, red/yellow discs, the disc-vs-ring rendering from §2.3).
- Reuses the platform layer entirely. **If you need to modify the platform to add it, that's a design bug — fix the seam first.**
- Landing grid grows to two cards.

**Exit criteria:** Connect 4 plays correctly end-to-end with clock and reconnect; no platform code was modified (only added).

**Sprint 4 — Tic-Tac-Toe 6×6 and 9×9 (~1 week).**

- Two more independent game modules. The work should feel mechanical.
- Catalog grid grows to four cards.

**Exit criteria:** All four MVP games are playable. Adding a game is a module choice, not a platform one.

**Sprint 5 — Rematch + resign (~1 week).**

- Rematch handshake: `OfferRematch`, `AcceptRematch`, `RejectRematch`. The asymmetric exit from §2.3 #10 (rejector auto-redirects; offerer stays with a notice and a manual exit button).
- Resign with the confirmation step (§2.3 #8).
- `Ended` and `AwaitingRematch` states wired per §2.9.
- **Series scoreboard** (§2.3 #13): server-side counter in the room state (`{ host, challenger, draws }`), updated on every `MatchEnded`, displayed in the in-match UI for both players. Reset only when the room reaches `Closed`/`Expired`.
- **Side swap on rematch** (§2.3 #15): on every accepted rematch, the server swaps `hostSide` and `challengerSide` before emitting `MatchStarted`. UI shows each player's current side in the HUD so the swap is obvious to both players.

**Exit criteria:** All four games can be played, resigned, finished, rematched (accepted/rejected), and exited cleanly.

**Sprint 6 — i18n + SEO + PWA + theming (~1–2 weeks).**

- i18next + `ka.json` and `en.json`. Every visible string moves behind a key.
- SEO: Next.js metadata, canonical, hreflang, sitemap, robots, JSON-LD on landing and per-game pages.
- PWA: manifest, icons, install prompt, service worker for offline shell.
- Theming: `next-themes`, semantic tokens in `globals.css`, light/dark/system, FOUC-prevention script.
- Accessibility pass: WCAG AA contrast in both themes, Connect 4 disc/ring legibility, focus rings, keyboard navigation.

**Exit criteria:** Lighthouse green (perf, a11y, SEO, best practices) on landing in both locales and both themes.

**Sprint 7 — Hardening for launch (~1 week).**

- Rate-limit policies on hot endpoints (§5.5).
- Security headers (CSP, HSTS, X-Frame-Options, etc.) — target A+ on `securityheaders.com`.
- PostHog instrumentation for every event from §4.2.
- Localized error codes (§3) end-to-end; friendly 404 / expired-room pages.
- Basic load test (~hundreds of concurrent rooms). Verify the API and Redis hold up.
- Production deploy with monitoring alerts wired to the on-call channel.

**Exit criteria:** Public launch on playme.ge at the cost target from §6.

**Roadmap rules:**

- **The first end-to-end slice (Sprint 1) is the canary** for whether the platform layer is right. Don't add anything to a later sprint until Sprint 1 ships.
- **No game-module work before Sprint 1.** Stub everything; defer until the platform skeleton is real.
- **Adding a new game (Sprints 3, 4) must be a pure addition.** If you modify the platform to add a game, fix the seam, then continue.
- **A sprint always lands a deployable, demonstrable slice.** Never split a sprint into "build, then make it work."
- **Sprint 7 is non-negotiable before public launch.** Going live without rate limits, CSP, or error monitoring is how products eat dirt.

---

## 12. Open Questions / Deferred to v2

Intentionally unresolved — raise in PRs rather than choosing silently.

- **Native mobile app** (React Native + Expo). Deferred to v2. `packages/shared` is already structured to be consumed by RN when added.
- **Monetization.** No monetization in v1. When introduced, the likely path is rewarded video ads first, then cosmetic IAP — both of which will require introducing optional accounts.
- **Accounts & player stats.** Not in v1 (pure anonymous play). Will become necessary when monetization, leaderboards, friends, or persistent history land.
- **Spectator mode.** Dropped from v1. Revisit after the core 1v1 flow is solid.
- **More games.** Catalog is fixed at four modules for MVP. New games are net-new modules, not parameterizations of existing ones.
- **Push notifications.** Web push only (where supported) when re-engagement becomes a priority. Native push waits for the mobile app.
- **Tournaments / prizes.** Not in v1. If pursued later, legal review is required (Georgian gambling-law implications even for skill-based paid entry).
- **Managed log/trace backend.** OTel currently exports to stdout/file. Wire to Grafana Cloud / Honeycomb / similar when scaling beyond one API instance.
- **Secrets vault.** Currently env vars on App Service / Vercel. Once secret count or rotation frequency justifies it, move the API to Azure Key Vault (managed identity → API → Key Vault). Until then, env vars are acceptable.
- **WAF / DDoS.** No WAF in v1. If abuse traffic shows up, put Azure Front Door / Cloudflare in front of the API (and re-evaluate rate-limit thresholds). Vercel already fronts the web.
- **On-call channel.** Where Sentry alerts route (Slack? Telegram? Email? Discord?) — pick one before public launch and document in §5.11.

When a decision is made, update the relevant section of this file in the same PR.
