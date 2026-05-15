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

- New self-contained game module `connect4` (gravity, red/yellow discs, the disc-vs-ring rendering from [`platform-and-games.md`](platform-and-games.md) §2.1).
- Reuses the platform layer entirely. **If you need to modify the platform to add it, that's a design bug — fix the seam first.**
- Landing grid grows to two cards.

**Exit criteria:** Connect 4 plays correctly end-to-end with clock and reconnect; no platform code was modified (only added).

**Sprint 4 — Tic-Tac-Toe 6×6 and 9×9 (~1 week).**

- Two more independent game modules. The work should feel mechanical.
- Catalog grid grows to four cards.

**Exit criteria:** All four MVP games are playable. Adding a game is a module choice, not a platform one.

**Sprint 5 — Rematch + resign (~1 week).**

- Rematch handshake: `OfferRematch`, `AcceptRematch`, `RejectRematch`. The asymmetric exit from [`platform-and-games.md`](platform-and-games.md) §1 #10 (rejector auto-redirects; offerer stays with a notice and a manual exit button).
- Resign with the confirmation step ([`platform-and-games.md`](platform-and-games.md) §1 #8).
- `Ended` and `AwaitingRematch` states wired per [`state.md`](state.md) §2.
- **Series scoreboard** ([`platform-and-games.md`](platform-and-games.md) §1 #13): server-side counter in the room state (`{ host, challenger, draws }`), updated on every `MatchEnded`, displayed in the in-match UI for both players. Reset only when the room reaches `Closed`/`Expired`.
- **Side swap on rematch** ([`platform-and-games.md`](platform-and-games.md) §1 #15): on every accepted rematch, the server swaps `hostSide` and `challengerSide` before emitting `MatchStarted`. UI shows each player's current side in the HUD so the swap is obvious to both players.

**Exit criteria:** All four games can be played, resigned, finished, rematched (accepted/rejected), and exited cleanly.

**Sprint 6 — i18n + SEO + PWA + theming (~1–2 weeks).**

- i18next + `ka.json` and `en.json`. Every visible string moves behind a key.
- SEO: Next.js metadata, canonical, hreflang, sitemap, robots, JSON-LD on landing and per-game pages.
- PWA: manifest, icons, install prompt, service worker for offline shell.
- Theming: `next-themes`, semantic tokens in `globals.css`, light/dark/system, FOUC-prevention script.
- Accessibility pass: WCAG AA contrast in both themes, Connect 4 disc/ring legibility, focus rings, keyboard navigation.

**Exit criteria:** Lighthouse green (perf, a11y, SEO, best practices) on landing in both locales and both themes.

**Sprint 7 — Hardening for launch (~1 week).**

- Rate-limit policies on hot endpoints ([`security.md`](security.md) §5).
- Security headers (CSP, HSTS, X-Frame-Options, etc.) — target A+ on `securityheaders.com`.
- PostHog instrumentation for every event from [`observability-and-i18n.md`](observability-and-i18n.md) §1.2.
- Localized error codes ([`observability-and-i18n.md`](observability-and-i18n.md) §2) end-to-end; friendly 404 / expired-room pages.
- Basic load test (~hundreds of concurrent rooms). Verify the API and Redis hold up.
- Production deploy with monitoring alerts wired to the on-call channel. Wired via `infra/provision.sh` (Azure CLI script) + `.github/workflows/deploy-api.yml` (GitHub Actions, OIDC → Azure, GHCR image). Alerts route to email; see [`security.md`](security.md) §11.

**Exit criteria:** Public launch on playme.ge at the cost target from the deployment table in `CLAUDE.md` §4.

### 1.1 Roadmap rules

- **The first end-to-end slice (Sprint 1) is the canary** for whether the platform layer is right. Don't add anything to a later sprint until Sprint 1 ships.
- **No game-module work before Sprint 1.** Stub everything; defer until the platform skeleton is real.
- **Adding a new game (Sprints 3, 4) must be a pure addition.** If you modify the platform to add a game, fix the seam, then continue.
- **A sprint always lands a deployable, demonstrable slice.** Never split a sprint into "build, then make it work."
- **Sprint 7 is non-negotiable before public launch.** Going live without rate limits, CSP, or error monitoring is how products eat dirt.

---

## 2. Open questions / deferred to v2

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
- ~~**On-call channel.**~~ Resolved: email. Sentry and Azure Monitor both route to the address configured in `infra/provision.env` (`ALERT_EMAIL`). Documented in [`security.md`](security.md) §11. Revisit if/when a team forms — Slack or a paging service makes more sense above one operator.

When a decision is made, update the relevant doc in the same PR.
