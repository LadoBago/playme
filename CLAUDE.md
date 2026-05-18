# CLAUDE.md

Context Claude Code needs to work effectively in **PlayMe**. Detailed specs live in `docs/` — **read the relevant one on demand**; this file is the index, not the spec.

---

## 1. Product

**PlayMe** (playme.ge) is a real-time, anonymous, two-player casual games platform. Host creates a match, picks the game + options, gets a shareable invite link, sends it to a friend. Both players play live over the network with a per-player chess clock.

Hard rules:

- **No accounts.** Anonymous; display name only. **Two players** per match, exactly. **Real-time only** over SignalR. **Invite-link matchmaking only** — no queue, no friends list, no leaderboards.
- **Catalog is fixed and small (MVP):** Tic-Tac-Toe 3×3 / 6×6 / 9×9, Connect 4. Each game is a **self-contained module** — no shared rules engine across games.
- **Languages at launch:** Georgian (`ka`, default) and English (`en`).
- **Client surface in v1: web only** (responsive Next.js + PWA). Native mobile is deferred to v2 — `packages/shared` is structured to be consumed by RN later.
- **No monetization in v1.**

---

## 2. Architecture (one screen)

```
                ┌──────────────────┐
                │   Vercel (CDN)   │
                │   Next.js (SSR)  │  ← web, responsive + PWA
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

- **API is stateless.** All match/room state lives in Redis. Any instance serves any client; no sticky sessions.
- **Redis = state store + SignalR backplane** via pub/sub. One instance, two roles.
- **SignalR** is the only real-time transport. One connection per active match.
- **Server is the single source of truth** for game state and the clock. Client moves are *proposals* the server validates and commits.

### Repo layout

```
/
├── apps/
│   ├── api/         # ASP.NET Core Web API (.NET 10)
│   └── web/         # Next.js (App Router) — SSR + SEO + PWA
├── packages/
│   ├── shared/      # TS types, Zod schemas, generated API client, SignalR wrapper, i18n
│   └── config/      # eslint, tsconfig, prettier base configs
├── infra/           # Redis docker-compose, Dockerfiles, deploy configs
├── tools/           # codegen scripts (OpenAPI → TS client)
├── docs/            # detailed specs (read on demand)
├── pnpm-workspace.yaml
├── turbo.json
└── CLAUDE.md
```

---

## 3. Detailed specs — read on demand

**You MUST consult the relevant doc(s) before changing anything in their scope.** This file's summaries are not a substitute for the canonical spec.

| Doc | Read when working on… |
|---|---|
| [`docs/architecture.md`](docs/architecture.md) | Clean Architecture layers, project structure, RoomHub method index, shared package, DTO source of truth |
| [`docs/frontend.md`](docs/frontend.md) | Web pages, SEO (metadata, hreflang, sitemap, JSON-LD, Core Web Vitals), theming, PWA |
| [`docs/platform-and-games.md`](docs/platform-and-games.md) | Platform invariants (clock, reconnect grace, rematch, side selection, scoreboard), per-game rules, cross-game UX, domain vocabulary |
| [`docs/state.md`](docs/state.md) | Redis key schema, room lifecycle / state machine, clock model, timeout sweeper, server-emitted events |
| [`docs/security.md`](docs/security.md) | Threat model, secrets, input validation, room/player identity & auth, rate limits, headers, CORS |
| [`docs/observability-and-i18n.md`](docs/observability-and-i18n.md) | Sentry, PostHog, Serilog, OTel, i18n catalogs, error code naming |
| [`docs/deployment.md`](docs/deployment.md) | **Production topology** (Vercel + Cloudflare + Azure West Europe), DNS/TLS, deploy pipeline, alerting, non-obvious decisions and gotchas, known follow-ups, cost |
| [`docs/roadmap.md`](docs/roadmap.md) | Implementation sprints, deferred-to-v2 questions, deferred-polish follow-ups |

---

## 4. Deployment

| Component | Platform | Tier (MVP) |
|---|---|---|
| `apps/web` (Next.js) | **Vercel** | Free tier |
| `apps/api` (ASP.NET Core) | **Azure App Service for Linux**, West Europe | B1 (~$13/mo) |
| Redis (state + SignalR backplane) | **Azure Cache for Redis**, West Europe | Basic C0 (~$15/mo) |
| DNS / TLS proxy for `api.playme.ge` | **Cloudflare** | Free tier |
| Sentry | Sentry Cloud | Free tier |
| PostHog | PostHog Cloud | Free tier |

- API ships with a Dockerfile from day one (local-dev / cloud parity, future container-platform portability).
- SignalR uses a **Redis backplane** via `Microsoft.AspNetCore.SignalR.StackExchangeRedis`. **No** Azure SignalR Service.
- Secrets via env vars in App Service / Vercel. Local dev: `appsettings.Development.json` + .NET user-secrets (API), `.env.local` gitignored (web).
- Cloudflare fronts `api.playme.ge` because Azure App Service Managed Certificate silently fails to provision on `.ge` TLDs. See [`docs/deployment.md`](docs/deployment.md) §6 for the full set of operational gotchas we hit during the v1 cutover.

---

## 5. Build / Test / Run

The repo uses **pnpm** + **Turborepo** for JS/TS, and the **dotnet CLI** for the API.

### Install
```bash
pnpm install
dotnet restore apps/api
```

### Run (dev)
```bash
pnpm dev                                            # turbo: starts web + watch tasks
pnpm --filter web dev                               # web only (Next.js dev server)
dotnet run --project apps/api                       # API on https://localhost:5001
docker compose -f infra/docker-compose.yml up redis # local Redis
```

### Build
```bash
pnpm build
pnpm --filter web build
dotnet build apps/api -c Release
docker build -f infra/api.Dockerfile -t playme-api .
```

### Test
```bash
pnpm test       # JS — Vitest (unit) + Playwright (e2e) preferred when adding tests
dotnet test     # .NET — xUnit + FluentAssertions + WebApplicationFactory
```

### Lint / format / typecheck
```bash
pnpm lint
pnpm format
pnpm typecheck
dotnet format apps/api
```

### Codegen
```bash
pnpm gen:api    # regenerates the TS API client from the API's OpenAPI doc
```

**Always run** `pnpm typecheck` and `pnpm lint` before declaring a frontend change done. **For backend, run** `dotnet build` and `dotnet format --verify-no-changes`.

---

## 6. Code style

### TypeScript (`apps/web`, `packages/shared`)

- `strict: true` everywhere. **No `any`** — use `unknown` and narrow.
- **Named exports** by default. Default exports only where frameworks require (Next.js page/layout).
- Async over `.then()`; no floating promises (every promise is awaited or explicitly `void`-ed with a comment).
- Validate every external input (HTTP responses, route params, env vars, SignalR messages) with **Zod**. Schemas in `packages/shared`.
- React: function components only, hooks at top level, co-locate component + styles + tests.
- State: prefer local state + URL state. Reach for Zustand only when state is genuinely cross-feature.
- Imports: path aliases (`@/features/...`, `@shared/...`); no deep relative imports (`../../../`).

### C# / .NET (`apps/api`)

- Target **.NET 10 (LTS)**. Pin via `global.json`. Nullable reference types **on** project-wide.
- **File-scoped namespaces**. One public type per file.
- Async all the way; no `.Result` / `.Wait()`. Pass `CancellationToken` through.
- DTOs are `record` types. Domain entities are classes with private setters + behavior, not anemic bags.
- Use `IOptions<T>` for configuration; never read `IConfiguration` in business code.
- Errors: throw domain exceptions for invariants; return `Result<T>` / `ProblemDetails` for expected failures. **No exceptions for control flow.**
- Logging: inject `ILogger<T>`; structured templates only.
- Naming: `PascalCase` public, `_camelCase` private fields, `camelCase` locals. Interfaces prefixed with `I`.
- Redis access via typed repositories in `Infrastructure/`. Hubs/controllers never touch `IConnectionMultiplexer` directly.

### SOLID (both sides)

Walk all five before merging:

- **S** — one reason to change. One handler per use case; one component renders one thing.
- **O** — extension over modification. Adding a new game module must NOT require editing the platform layer or other modules. If you find yourself editing a platform-layer `switch` statement to add a game, redesign the seam.
- **L** — implementations honor interface contracts (same exceptions, same idempotency, same null/empty semantics).
- **I** — narrow interfaces (`IRoomRepository` + `IClockService`, not one fat `IGameService`). Props/hook signatures expose only what the caller needs.
- **D** — depend on abstractions. `Application` defines ports; `Infrastructure` implements. Never `new` infrastructure inside a handler. Wrap externals on the web (analytics, SignalR connection) so swapping the adapter is a one-file change.

If a SOLID violation is necessary, call it out in the PR description with a one-line rationale and (where possible) a follow-up ticket.

### Shared

- Every file ends with a newline; no trailing whitespace.
- No commented-out blocks. `TODO` only with a ticket reference (`// TODO(#123): ...`).
- Public APIs (exported TS, public C#) have doc comments explaining intent, not just restating the signature.
- **No hard-coded user-facing strings** — always through an i18n key.

---

## 7. Workflow rules

### Branching

- **Trunk-based.** `main` is always releasable.
- Branches: `feat/<slug>`, `fix/<slug>`, `chore/<slug>`. Short-lived (< 3 days).
- Never commit directly to `main`. Every change goes through a PR.

### Commits

- **Conventional Commits**: `feat:`, `fix:`, `chore:`, `refactor:`, `docs:`, `test:`, `perf:`.
- Subject imperative mood, ≤ 72 chars. Body explains *why*, not *what*.
- Squash-merge PRs into `main`.

### Pull requests

- One logical change per PR. Past ~400 changed lines, split it.
- Description: what changed, why, how it was tested, screenshots/recordings for UI, follow-ups left behind.
- CI must be green: lint, typecheck, `dotnet build`, `dotnet format --verify-no-changes`, tests (once they exist).
- At least one review approval before merge.

### What Claude Code does automatically

- Runs `pnpm typecheck` + `pnpm lint` after non-trivial TS edits.
- Runs `dotnet build` after non-trivial C# edits.
- Updates / adds Zod schemas in `packages/shared` when API contracts change; regenerates the TS API client (`pnpm gen:api`).
- Adds/updates i18n keys in **both** `ka.json` and `en.json` when introducing new UI text.
- Treats the **server as source of truth** for game state and clock. Never adds client-side game logic that could disagree.
- When touching the platform layer (rooms, clock, reconnect, rematch), verifies every game module still behaves correctly.
- New backend feature → logic in an `Application/` **handler** (Command or Query), called from a thin controller or hub.
- New external service → define the interface in `Application/Abstractions/` first, implement it in `Infrastructure/`, register in DI. `Application`/`Domain` never import a third-party SDK directly.
- Walks SOLID before merging (see §6).
- New / changed public page → full SEO surface (metadata, canonical, OG, Twitter, robots, hreflang, sitemap entry). Treat a page without metadata as incomplete.
- Updates `sitemap.ts` whenever a new public, indexable route is added or removed.
- Validates every external input: Zod on web, FluentValidation on API. No exceptions.
- Authorizes every Hub method + controller action by session token + room role (see [`docs/security.md`](docs/security.md) §4).
- Uses cryptographic RNG (`RandomNumberGenerator` in C#, `crypto.getRandomValues` on web) for tokens, room codes, signed values. Never `Math.random()`, `Guid.NewGuid()`, or `DateTime`-derived values.
- After security-relevant changes (auth, CORS, headers, rate limits, dependency bumps): `pnpm audit --prod` + `dotnet list package --vulnerable --include-transitive`; recheck headers via `securityheaders.com`.
- When unsure about a tradeoff affecting the platform layer or more than one game, **stops and asks** rather than guessing.

### Platform thinness (the open-closed rule, made concrete)

The platform layer (`PlayMe.Domain/Platform/`, the platform handlers in `PlayMe.Application/`, the SignalR `RoomHub`, the Redis serialization) is the **skeleton** for the project's class of games: turn-based, two-player, clocked. It owns: room/match lifecycle, presence/reconnect, the chess clock, side assignment, and the move-pipeline shell (auth → distributed lock → dispatch → broadcast). **Nothing else.**

- **Adding a new in-scope game (TTT 6×6 / 9×9, Connect 4, chess, …) must touch zero platform code.** The diff is a new `Domain/Games/<game>/` folder, a new `IGameModule` + `IGameMoveParser`, DI registration, a catalog entry, and per-game web rendering. If a new game forces an edit in `Domain/Platform/`, in the platform handlers, in the Hub, or in `Infrastructure/Redis/`, **the seam is wrong — fix the seam first, then add the game.**
- **The platform changes only when its own scope changes** — supporting 3+ players, dropping the clock, allowing simultaneous (non-turn-based) play. Those are explicit, deliberate, discussed changes; never a side-effect of adding a game.
- **Per-game code duplication is acceptable and expected.** Two games independently implementing "find N-in-a-row on a grid" is fine. **Do not grow the platform to host shared helpers** — that's how skeletons grow fat and tightly couple every future game to assumptions made for an earlier one.
- **For genuine commonality between games, prefer composition over inheritance.** Expose a small utility (e.g. `GridLineDetector`) that a game module *chooses to instantiate and use*, never as platform behavior every module inherits or that the platform routes through. The platform must not know the helper exists.
- **Game-specific vocabulary stays inside the module:** move payloads, move-reject keys, board shape, side identifiers ("x"/"o" vs "red"/"yellow"), state encoding. The platform sees only opaque types — `IGameState` (marker), `GameMove` (abstract record), `IGameModule.Serialize`/`Deserialize`, and reject keys that are an **agreement between the per-game server module and the per-game web renderer** (not platform-owned i18n keys, not a platform enum).

The test: **could this code work unchanged if we removed every game except chess?** If yes, it's platform. If no, it's a game module.

### What Claude Code MUST NOT do

- Don't introduce a shared rules engine across game modules. Each game is intentionally self-contained.
- Don't switch to minimal APIs. The API uses full controllers (`ControllerBase` + `[ApiController]`).
- Don't ship a new public page without metadata, canonical URL, hreflang alternates, and a sitemap entry. SEO is a hard requirement, not a polish item.
- Don't make `/r/<roomId>` (rooms) indexable. Room pages are `noindex, nofollow` always.
- Don't use sequential or guessable room IDs in URLs. Room codes are opaque tokens.
- Don't render public pages client-only. Public pages are SSR or SSG; only the in-match UI is CSR.
- Don't load custom fonts or images without `next/font` / `next/image` — Core Web Vitals (LCP, CLS) are part of the SEO contract.
- Don't hard-code colors (hex / rgb / Tailwind literals like `bg-blue-500`) in components. Always go through a semantic theme token so both light and dark modes work.
- Don't ship a UI change without verifying it in **both** light and dark mode, and with the OS set to each preference.
- Don't use `Math.random()`, `Guid.NewGuid()`, or `DateTime`-derived values for security tokens, room codes, or anything that needs to be unguessable.
- Don't render arbitrary user input through `dangerouslySetInnerHTML`. No exceptions for "trusted" inputs.
- Don't log session tokens, invite codes, Redis URIs, or other secrets at any level. Review log statements that include request bodies or DTOs.
- Don't widen CORS to `*` or weaken CSP "temporarily." Add the specific origin / source explicitly.
- Don't add a new third-party JS dependency to the web bundle without considering CSP impact, supply-chain risk, and runtime load.
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
- Don't add a periodic `ClockTick` broadcast. By design the server is **event-driven only**: every state-mutating event carries a `ClockSnapshotDto` and the web client interpolates locally at ~10 Hz. The `ClockTick` event name is reserved for a possible future drift-correction sweep; do not implement it without a concrete drift symptom (see [`docs/state.md`](docs/state.md) §2.2).
- Don't add user-facing strings without translations in both locales.
- Don't introduce new top-level dependencies without flagging them in the PR description (license, size, maintenance).
- Don't change CI configuration, release scripts, `turbo.json`, or the deploy targets silently — call them out.
- Don't commit secrets, `.env` files, or local certificates.

---

## 8. Quick reference — where things live

| Concern | Location |
|---|---|
| Shared TS types & Zod schemas | `packages/shared/src/` |
| Generated API client | `packages/shared/src/api/` (do not edit by hand) |
| SignalR client wrapper | `packages/shared/src/realtime/` |
| Translation catalogs | `packages/shared/src/i18n/{ka,en}.ts` (game rules live under the `games.*.rules` keys) |
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
