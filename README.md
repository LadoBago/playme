# PlayMe

Real-time, anonymous, two-player casual games platform. Host picks a game, shares an invite link, both players play live over the network with a per-player chess clock.

Live at [playme.ge](https://playme.ge).

## What's in the catalog (MVP)

- Tic-Tac-Toe 3×3 / 6×6 / 9×9
- Connect 4

Each game is a self-contained module. No accounts, no queue, no leaderboards — invite-link matchmaking only. Languages: Georgian (default) and English.

## Architecture

```
Vercel (Next.js, SSR + PWA)  ──HTTPS / WSS──▶  Azure App Service (ASP.NET Core, .NET 10)
                                                         │
                                                         ▼
                                              Azure Cache for Redis
                                              (state store + SignalR backplane)
```

- API is stateless; all match state lives in Redis.
- SignalR is the only real-time transport, with Redis as the backplane (no Azure SignalR Service).
- Server is the single source of truth for game state and the clock.

## Repo layout

```
apps/
  api/              ASP.NET Core Web API (.NET 10)
  web/              Next.js App Router — SSR + SEO + PWA
packages/
  shared/           TS types, Zod schemas, generated API client, SignalR wrapper, i18n
  config/           shared eslint / tsconfig / prettier
infra/
  api.Dockerfile, docker-compose.yml, provision.sh
  loadtest/         @playme/loadtest workspace
docs/               canonical specs — read these before changing anything in their scope
CLAUDE.md           working agreement for Claude Code in this repo
```

## Prerequisites

- Node ≥ 20, pnpm ≥ 10
- .NET 10 SDK (pinned via `global.json`)
- Docker (for local Redis)

## Getting started

```bash
pnpm install
dotnet restore apps/api

docker compose -f infra/docker-compose.yml up redis    # local Redis
dotnet run --project apps/api/src/PlayMe.Api           # API on http://localhost:5080
pnpm --filter web dev                                  # web on http://localhost:3000
```

Or run the JS side together via Turborepo:

```bash
pnpm dev
```

## Common tasks

| Task | Command |
|---|---|
| Build everything | `pnpm build` and `dotnet build apps/api -c Release` |
| Typecheck (web/shared) | `pnpm typecheck` |
| Lint / format | `pnpm lint` / `pnpm format` |
| Test (JS) | `pnpm test` |
| Test (.NET) | `dotnet test` |
| Format check (.NET) | `dotnet format apps/api --verify-no-changes` |
| Regenerate TS API client | `pnpm gen:api` |
| Build API container | `docker build -f infra/api.Dockerfile -t playme-api .` |

## Documentation

Detailed specs live in [`docs/`](docs/). Read the relevant one before touching the area it covers.

| Doc | Scope |
|---|---|
| [architecture.md](docs/architecture.md) | Clean Architecture layers, `RoomHub` surface, shared package, DTO source of truth |
| [frontend.md](docs/frontend.md) | Web pages, SEO, theming, PWA |
| [platform.md](docs/platform.md) | Platform invariants (clock, reconnect, rematch, sides, scoreboard), game-module catalog |
| [games/](docs/games/) | Canonical per-game rules and module specs (one file per game) |
| [state.md](docs/state.md) | Redis key schema, room lifecycle, clock model, server events |
| [security.md](docs/security.md) | Threat model, auth, rate limits, headers, CORS |
| [observability-and-i18n.md](docs/observability-and-i18n.md) | Sentry, PostHog, Serilog, OTel, i18n catalogs |
| [deployment.md](docs/deployment.md) | Production topology, DNS/TLS, deploy pipeline, gotchas, cost |
| [loadtest.md](docs/loadtest.md) | Load-test harness |
| [roadmap.md](docs/roadmap.md) | Sprints, deferred items |

## Contributing

Trunk-based. Branches `feat/<slug>`, `fix/<slug>`, `chore/<slug>`, short-lived. Conventional Commits, squash-merged PRs, one logical change per PR. CI (lint + typecheck + `dotnet build` + `dotnet format --verify-no-changes` + tests) must be green.

**Never commit or push directly to `main`** — including docs, README, and one-line fixes. Always branch, open a PR, squash-merge.

The full working agreement — code style, SOLID expectations, platform thinness, what must not be done — is in [CLAUDE.md](CLAUDE.md). It applies to humans too.
