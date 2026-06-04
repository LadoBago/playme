# Sprint 0 — Bootstrap (~1 week)

A hello-world that exercises every piece of infrastructure.

- Initialize the monorepo (pnpm + Turborepo) per [`architecture.md`](../architecture.md) §2.
- Scaffold `apps/api` with the four Clean Architecture projects (`PlayMe.Domain`, `PlayMe.Application`, `PlayMe.Infrastructure`, `PlayMe.Api`); project references enforce the dependency rule.
- Scaffold `apps/web` (Next.js App Router, TS strict) with a placeholder landing page.
- Scaffold `packages/shared` (TS-only) and `packages/config` (base eslint/prettier/tsconfig).
- `infra/docker-compose.yml` for local Redis. Wire the Redis backplane in SignalR.
- `.editorconfig`, GitHub Actions CI (lint + typecheck + `dotnet build` + `dotnet format --verify-no-changes`).
- Deploy: API to Azure App Service for Linux, web to Vercel.
- Wire Sentry on both ends, PostHog on the web (no events yet).
- Trivial `GET /api/health` endpoint called by the landing page.

**Exit criteria:** Both ends boot, deploys are green, web reaches the API over WSS, Sentry sees a deliberate test error.
