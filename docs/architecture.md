# Architecture

Detailed architecture spec for PlayMe. Covers Clean Architecture layers, project structure, RoomHub method index, and the shared package. For platform invariants and game rules see [`platform-and-games.md`](platform-and-games.md). For frontend specifics see [`frontend.md`](frontend.md). For Redis schema and state machine see [`state.md`](state.md).

---

## 1. System architecture

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

## 2. Repository layout (monorepo)

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
├── docs/            # detailed specs (this folder)
├── pnpm-workspace.yaml
├── turbo.json
└── CLAUDE.md
```

- `apps/mobile/` is **deferred to v2** (React Native + Expo). Do not create it yet, but design shared code so it can be added without a refactor.
- `packages/shared/` exists in v1 even though only `apps/web/` consumes it. It's the foundation for cross-platform code sharing when mobile lands.
- `infra/` holds the `docker-compose.yml` for local Redis, the API `Dockerfile`, and any deploy descriptors. Redis is never run via a separate code module — it's pure infrastructure.

## 3. Backend architecture (apps/api)

- **ASP.NET Core Web API** with **full controllers** (`ControllerBase` + `[ApiController]` attribute routing). **No minimal APIs.** Controllers give us clearer per-endpoint discoverability, attribute-based authorization, model binding, filters, and route organization as the API grows.

### 3.1 Clean Architecture

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

### 3.2 Project structure

One `.csproj` per layer:

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

### 3.3 RoomHub method index

Semantic index of all Hub methods. **Literal C# signatures live in `RoomHub.cs`** — this table is the source-of-truth for *what methods exist and when each is valid*, not for their exact parameter types (those are C# records in `Application/`). Every method runs the authorization check from [`security.md`](security.md) (§4) before any other logic.

| Method | Valid when | Effect | Server emits |
|---|---|---|---|
| `JoinRoom` | On SignalR connect; room in `WaitingForOpponent`, `InProgress`, `Ended`, or `AwaitingRematch` | Registers presence; reattaches via session cookie | `OpponentJoined` (challenger's first join), `OpponentReconnected` (reconnect during `InProgress`) |
| `SubmitMove` | Room `InProgress`; caller is active player; effective clock > 0 | Validates move via Domain rules; applies; flips turn; reschedules timeout | `MoveAccepted` (both), `MoveRejected` (caller only), `MatchEnded` if win/draw |
| `Resign` | Room `InProgress`; caller is in the match | Ends match | `MatchEnded(Outcome.Resign(caller))` |
| `OfferRematch` | Room `Ended` (creates offer) OR `AwaitingRematch` from responder (implicit accept) | Records offer or starts new match | `RematchOffered`, or `MatchStarted` on implicit accept |
| `AcceptRematch` | Room `AwaitingRematch`; caller is responder (not offerer) | Starts new match with swapped sides | `MatchStarted` |
| `RejectRematch` | Room `AwaitingRematch`; caller is responder | Closes the room; rejector auto-routed | `RematchDeclined` to offerer |
| `ExitRoom` | Room `Ended` or `AwaitingRematch` | Transitions room to `Closed` | `OpponentExited` to the still-present player |

When adding a new Hub method: append a row here, then implement.

## 4. Shared package (packages/shared)

- **TypeScript types** for game state, moves, room state, clock state.
- **Zod schemas** for validating any external input (route params, env vars, server-pushed messages).
- **Generated API client** from the API's OpenAPI document (`tools/gen-api`).
- **SignalR client wrapper** — typed wrapper around `@microsoft/signalr` with reconnect logic.
- **i18n catalogs** for `ka` and `en`. Translation keys are defined here so future mobile reuses them.
- **No React, Next.js, or React Native imports** allowed in `packages/shared`. Pure TS only.
- **DTO source of truth.** DTO shapes are owned by **C# records in `PlayMe.Application/`**. The generated TS client (`pnpm gen:api` consuming the API's OpenAPI document) is the canonical mirror for the web. Zod schemas in `packages/shared/src/schemas/` validate inputs at runtime (URL params, forms, server-pushed SignalR messages). CLAUDE.md describes *field-level contracts inline* only when the API surface is decision-worthy (e.g. the join body in [`frontend.md`](frontend.md) §1). Literal record definitions belong in code. **Do not transcribe DTO schemas into docs** — they go stale instantly.
