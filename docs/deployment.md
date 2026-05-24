# Deployment

The production stack for **PlayMe** as it actually exists. The shape is the deliberate kind — it works around real things we hit while bringing the v1 up. State + traps, not a step-by-step runbook (the scripts under `infra/` are the runbook; runbooks rot, the reasoning behind them is what stays useful).

For the API/web split itself, see [`architecture.md`](architecture.md). For security headers and CORS policy, [`security.md`](security.md). For roadmap context, [`roadmap.md`](roadmap.md).

---

## 1. Topology

```
                 Tbilisi user
                      │
       ┌──────────────┴──────────────┐
       │                             │
       ▼                             ▼
  www.playme.ge                api.playme.ge
   (Vercel edge)             (Cloudflare proxy)
       │                             │
       │ Next.js SSR +               │ HTTPS + WSS
       │ static assets               ▼
       ▼                       Azure App Service for Linux
   Vercel build               (West Europe, B1, Linux container)
                                    │
                                    ▼
                          Azure Cache for Redis
                          (West Europe, Basic C0,
                          state + SignalR backplane)
```

`www.playme.ge` is served by Vercel directly. `api.playme.ge` is fronted by Cloudflare, which proxies to the Azure App Service. The two share eTLD+1 (`playme.ge`), so the session cookie set by the API is first-party from the web's perspective and `SameSite=Lax` works (see [`security.md`](security.md) §4).

---

## 2. Components

| Component | Provider | Tier | Region | Notes |
|---|---|---|---|---|
| Web (`apps/web`) | **Vercel** | Hobby (free) | auto / closest edge | Next.js SSR + static. Custom domain: `www.playme.ge`, with apex redirect. |
| API (`apps/api`) | **Azure App Service for Linux** | B1 (~$13/mo) | West Europe | Runs the GHCR-hosted container image. WebSockets + Always On + HTTPS-only + TLS 1.2 + system-assigned managed identity. |
| Redis | **Azure Cache for Redis** | Basic C0 (~$15/mo) | West Europe | TLS-only, port 6380. State store + SignalR backplane. |
| Container registry | **GitHub Container Registry (GHCR)** | free, **public package** | n/a | `ghcr.io/ladobago/playme-api`. App Service pulls anonymously — no PAT to rotate. |
| DNS authority | **Cloudflare** | free | n/a | Moved from Vercel's nameservers during the v1 cutover (see §6 below). |
| TLS — `api.playme.ge` (edge) | **Cloudflare Universal SSL** (Let's Encrypt) | free | edge | Cloudflare terminates TLS at the edge. |
| TLS — `api.playme.ge` (origin) | **Azure App Service Managed Certificate** (DigiCert) | free | West Europe | Bound to the App Service via SNI. CF→origin runs in **Full (strict)** — CF validates the origin hostname against this cert. |
| TLS — `www.playme.ge` / apex | **Vercel** (Let's Encrypt) | free | edge | Vercel issues automatically against the domain claim. |
| Error monitoring | **Sentry Cloud** | free | EU | One project for web, one for API. |
| Product analytics | **PostHog Cloud** | free | EU | Web + API. The API requires `PostHog__ApiKey` on the App Service or it silently no-ops server-emitted events (§6.12). |
| Alerting | **Azure Monitor + email action group** | free | n/a | See §5. |

---

## 3. DNS / TLS

Cloudflare is the authoritative DNS for `playme.ge`. Records currently in the zone:

| Type | Name | Target | Proxy | Purpose |
|---|---|---|---|---|
| CNAME | `api` | `playme-api-prod.azurewebsites.net` | Proxied (orange) | API. Cloudflare terminates TLS for `api.playme.ge`. |
| A | `playme.ge` (apex) | Vercel edge IPs | Proxied | Apex domain — Vercel redirects to `www`. |
| A | `www` | Vercel edge IPs | Proxied | Web. |
| CAA | `playme.ge` | `letsencrypt.org`, `pki.goog`, `sectigo.com`, `comodoca.com`, `digicert.com`, `ssl.com` (issue + issuewild, minus `sectigo.com` on issuewild) | DNS-only | Restricts which CAs may issue certs. Covers the actual issuers in use: Let's Encrypt (Vercel apex/www), Google/Sectigo (Cloudflare edge), DigiCert (Azure managed cert for `api`). |

The Azure App Service has `api.playme.ge` bound as a custom hostname even though TLS is terminated at Cloudflare — App Service routes by `Host` header, so the binding must exist or it returns "404 Web Site not found".

---

## 4. Deploy pipeline

`.github/workflows/deploy-api.yml` ships the API on every push to `main` that touches `apps/api/**` or `infra/api.Dockerfile`:

1. Build the image from `infra/api.Dockerfile`.
2. Push to `ghcr.io/ladobago/playme-api:<sha>` and `:latest`.
3. Auth to Azure via **OIDC federated credential** (no long-lived secrets in repo). The federated credential is on the `playme-github-deploy` Azure AD app, scoped to `repo:LadoBago/playme:ref:refs/heads/main`.
4. `az webapp config container set` repoints the App Service at the new tag, then restart.
5. Smoke-check `/api/health` 10× with backoff.

The web is deployed by **Vercel's GitHub integration** on every push to `main`, no workflow file in the repo. Vercel reads `apps/web` as the project root; env vars live in Vercel project settings.

**Provisioning** is `infra/provision.sh` — idempotent Azure CLI script that materialises everything from a fresh subscription. Supports `PROVISION_PHASE=resources|domain|all` so the long Redis create (~15 min) can run unattended in CI. Reads `infra/provision.env` (gitignored; template in `provision.env.example`).

---

## 5. Alerting

Sentry alerts and Azure Monitor alerts both route to the address in `infra/provision.env` (`ALERT_EMAIL`). For v1 this is a single solo-operator inbox; revisit if a team forms (Slack or a paging service makes sense then).

Azure Monitor alert rules wired by `provision.sh`:

- **`playme-api-5xx`** — total `Http5xx` > 10 / 5 min, severity 2.
- **`playme-api-slow`** — avg `HttpResponseTime` > 2 s / 5 min, severity 3.
- **`playme-redis-high-load`** — avg `serverLoad` > 90% / 5 min, severity 2.

All three notify via the `playme-oncall` action group (email).

---

## 6. Non-obvious decisions and gotchas

These are the things we lost real time to during the v1 cutover. Pinning them here so the next person (likely future-you) doesn't.

### 6.1 Cloudflare in front of the API

The original plan in [`CLAUDE.md`](../CLAUDE.md) §4 had the API at `api.playme.ge` via an Azure App Service Managed Certificate — the standard free-TLS path. For most of v1's life this didn't work: **the managed-cert pipeline silently failed on `.ge` ccTLDs** in both Italy North and West Europe. The PUT returned `202 Accepted` with an operation ID, the resource never materialised, the Portal also failed with "unknown error", and there was no escalation path without a paid support plan.

Azure quietly fixed this on the West Europe side. We re-tried on **2026-05-18** and provisioning succeeded on first attempt — the cert is DigiCert-issued, bound to `api.playme.ge` via SNI, and CF→origin now runs in **Full (strict)** mode (it validates the origin hostname against the managed cert instead of accepting any `*.azurewebsites.net` cert).

That removed the **original** reason for Cloudflare. But CF stays in front of the API for two reasons that haven't changed:

- **WebSocket proxying.** Vercel's external rewrites returned HTTP 400 on the WS upgrade and broke SignalR (see §6.2). CF proxies WS cleanly.
- **POP proximity to Tbilisi.** Cloudflare has edge POPs much closer to Georgian users than Azure West Europe — real latency win for the realtime path.

So the topology is unchanged; what changed is that the origin now presents a valid hostname cert and strict mode is on.

If we ever do want to drop CF entirely (cost, simplification, or because realtime moves to a different transport), the path is clean now: flip the CF CNAME to DNS-only and you're hitting Azure directly with a valid cert. Cost of leaving: lose WS proxying for Vercel-style rewrites (irrelevant if DNS is direct) and the Tbilisi POPs.

### 6.2 Vercel external rewrites don't proxy WebSocket

We initially routed `api.playme.ge` through a Vercel project-domain rewrite. **Vercel returns HTTP 400 on the WebSocket upgrade** for external destinations; SignalR falls back to Server-Sent Events or long-polling, and in-match moves visibly lag (a clear ~500 ms gap per event). Cloudflare proxies WS cleanly. This is why we moved off Vercel for `api.playme.ge` specifically.

### 6.3 West Europe, not Italy North

Italy North is closer to Tbilisi (~50 ms vs ~70 ms) and was our first pick. Several preview features lag there — App Service Managed Cert, certain CLI flags — and we hit the same silent-fail behavior described in §6.1. **West Europe (Amsterdam)** is one of Azure's oldest regions; preview features are GA there.

### 6.4 Turborepo strips env vars from build tasks by default

`next.config.js` is evaluated at build time. Vercel-set env vars **don't reach the build process** unless they're declared in `turbo.json`'s `build.env` array — Turborepo strips everything else for cache-correctness. The Vercel build log warns about this explicitly, but the warning is easy to miss in 100 lines of pnpm install output. See `turbo.json`.

### 6.5 Vercel "Sensitive" env vars can't be un-flagged

Marking an env var as **Sensitive** in Vercel makes it unavailable to `next.config.js` at build time, and **also blocks `NEXT_PUBLIC_*` client-bundle inlining** (the entire point of the prefix). The Sensitive flag cannot be toggled off — you must **delete the variable and re-create it** without the flag. The UI gives no clear error if you try. This is what caused several false starts during the Vercel cutover; the build looked clean and the env var "existed", but its value was empty at build evaluation. Resolved for the three telemetry vars (`NEXT_PUBLIC_SENTRY_DSN`, `NEXT_PUBLIC_POSTHOG_KEY`, `NEXT_PUBLIC_POSTHOG_HOST`) — they're now reaching the client bundle. Keep an eye out if you add new `NEXT_PUBLIC_*` vars later: don't tick Sensitive.

### 6.6 Container needs a writable home for Data Protection

`infra/api.Dockerfile` creates a non-root user `playme`. We originally used `--no-create-home`, and every `POST /api/rooms` 500'd because ASP.NET Core's Data Protection wrote its key ring under `$HOME/.aspnet/DataProtection-Keys` on the first cookie mint, and `/home/playme` didn't exist. Now `--create-home`. Keys are now persisted to Redis (see §7), so the filesystem path is unreachable in practice, but the writable home stays as a belt-and-braces fallback in case a future framework default or DI mistake routes around our `IXmlRepository`.

### 6.7 Hostname binding doesn't survive RG re-creation

When we tore down Italy North and re-provisioned in West Europe, the `api.playme.ge` custom-hostname binding on the new App Service had to be re-added manually — the script's `az webapp config hostname add` step is gated by the DNS-records pause that we'd already done before, so the second re-provisioning run skipped it. If you re-provision, **double-check `az webapp config hostname list` returns both `*.azurewebsites.net` and `api.playme.ge`** before declaring done.

### 6.8 AAD role assignment dies with the resource group

The `playme-github-deploy` AAD app's `Contributor` role is scoped to the resource group ID. Deleting and recreating the RG (even with the same name) **destroys the role assignment** — the federated credential survives at tenant level but the deploy workflow then fails with "No subscriptions found" at the OIDC login step. `provision.sh` re-creates it in the domain phase. If you split phases, run the domain phase too.

### 6.9 ISP DNS caching beyond TTL

Several Georgian ISPs cache DNS far beyond the published TTL (we observed ~hours past TTL expiry). When the `api.playme.ge` record moves between providers, users on those ISPs see stale resolution and the browser surfaces it as a CORS error (because the stale destination has no project for the hostname anymore). For testing: switch to `1.1.1.1` at the OS level or enable Chrome's Secure DNS (DoH). For real users: there's no server-side fix; just wait it out or, during a planned migration, leave both old and new paths working until caches age out.

### 6.10 `az redis create` is synchronous in the current CLI

The CLI no longer accepts `--no-wait` on `az redis create` (this changed at some point and provisioning hangs for ~15 min). `provision.sh` works around it by spawning the create as a shell `&` background job and `wait`-ing later — preserves parallelism with the App Service work.

### 6.11 ARM eventual consistency on `webapp config set`

Right after `az webapp create`, the immediate next `az webapp config set` can 404 even though the resource exists. `provision.sh` blocks with `az webapp wait --created` before configuring.

### 6.12 API PostHog client silently no-ops without `PostHog__ApiKey`

The API's analytics adapter falls back to `NoOpAnalyticsClient` whenever `PostHog__ApiKey` is empty on the App Service (`AddInfrastructure.cs:74-81`). Intentional for local dev — invisible in prod. We hit this when reversi matches weren't appearing in PostHog Trends: web-emitted events (`match_started`, `room_created`, `rematch_*`) flowed, server-emitted events (`match_ended`, `room_expired`) were dropped on the floor.

Fix is one env var on the App Service:

```
PostHog__ApiKey = <project API key, same phc_… value as NEXT_PUBLIC_POSTHOG_KEY>
```

`__` (double underscore) maps to `PostHog:ApiKey` in ASP.NET config. Restart the App Service after setting it. Host defaults to `https://eu.i.posthog.com`; set `PostHog__Host` only if you're on a different region. Historical events from before the var was set are unrecoverable.

---

## 7. Known follow-ups

These aren't blocking launch but each is on the list:

- ~~**Persist Data Protection keys to Redis.**~~ Done. `Microsoft.AspNetCore.DataProtection.StackExchangeRedis` is wired in `AddApi.cs` against the shared `IConnectionMultiplexer`; keys live at `playme:dp-keys` in the same Redis we use for state + the SignalR backplane. `SetApplicationName("playme-api")` namespaces them. Session cookies survive container restarts/redeploys; the key ring is also implicitly shared if we ever horizontally scale.
- ~~**Re-create the Sensitive-flagged Vercel env vars.**~~ Done — `NEXT_PUBLIC_SENTRY_DSN`, `NEXT_PUBLIC_POSTHOG_KEY`, `NEXT_PUBLIC_POSTHOG_HOST` are now inlined into the client bundle.
- ~~**Revisit Azure App Service Managed Certificate.**~~ Done on 2026-05-18 — `.ge` provisioning now works, cert is bound to `api.playme.ge` via SNI, CF→origin upgraded to **Full (strict)**. CF stays in front for WebSocket proxying (§6.2) and Tbilisi-POP latency, not for TLS termination. Path to dropping CF entirely is now clean if we ever want it (see §6.1).
- **Move on-call channel beyond email** when a team forms. See [`security.md`](security.md) §11 / [`roadmap.md`](roadmap.md) §2.
- **Native-speaker pass over the rest of `packages/shared/src/i18n/ka.ts`.** Two real Georgian issues slipped past mechanical reviews; the remaining ~100 keys may have similar ones.

---

## 8. Cost (MVP)

| Item | Provider | Monthly |
|---|---|---|
| Web hosting | Vercel Hobby | $0 |
| API hosting | Azure App Service B1, Linux | ~$13 |
| Redis | Azure Cache for Redis Basic C0 | ~$15 |
| DNS / TLS proxy | Cloudflare Free | $0 |
| Container registry | GHCR (public) | $0 |
| Error monitoring | Sentry Free | $0 |
| Product analytics | PostHog Free | $0 |
| **Total** | | **~$28** |

This is the v1 budget. The first real cost step is App Service B1 → S1 (~$70/mo) if we need better cold-start or more headroom, which we don't yet.
