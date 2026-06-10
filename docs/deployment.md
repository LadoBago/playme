# Deployment

The production stack for **PlayMe** as it actually exists. The shape is the deliberate kind — it works around real things we hit while bringing the v1 up. State + traps, not a step-by-step runbook (the scripts under `infra/` are the runbook; runbooks rot, the reasoning behind them is what stays useful).

For the API/web split itself, see [`architecture.md`](architecture.md). For security headers and CORS policy, [`security.md`](security.md). For roadmap context, [`roadmap/`](roadmap/).

---

## 1. Topology

```
                 Tbilisi user
                      │
       ┌──────────────┴──────────────┐
       │                             │
       ▼                             ▼
    playme.ge                  api.playme.ge
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

`playme.ge` (the canonical apex) is served by Vercel directly; `www.playme.ge` 308-redirects to it. `api.playme.ge` is fronted by Cloudflare, which proxies to the Azure App Service. The two share eTLD+1 (`playme.ge`), so the session cookie set by the API is first-party from the web's perspective and `SameSite=Lax` works (see [`security.md`](security.md) §4).

---

## 2. Components

| Component | Provider | Tier | Region | Notes |
|---|---|---|---|---|
| Web (`apps/web`) | **Vercel** | Hobby (free) | auto / closest edge | Next.js SSR + static. Canonical domain: `playme.ge` (apex); `www.playme.ge` 308-redirects to it. |
| API (`apps/api`) | **Azure App Service for Linux** | B1 (~$13/mo) | West Europe | Runs the GHCR-hosted container image. WebSockets + Always On + HTTPS-only + TLS 1.2 + system-assigned managed identity. |
| Redis | **Azure Cache for Redis** | Basic C0 (~$15/mo) | West Europe | TLS-only, port 6380. State store + SignalR backplane. |
| Container registry | **GitHub Container Registry (GHCR)** | free, **public package** | n/a | `ghcr.io/ladobago/playme-api`. App Service pulls anonymously — no PAT to rotate. |
| DNS authority | **Cloudflare** | free | n/a | Moved from Vercel's nameservers during the v1 cutover (see §6 below). |
| TLS — `api.playme.ge` (edge) | **Cloudflare Universal SSL** (Let's Encrypt) | free | edge | Cloudflare terminates TLS at the edge. |
| TLS — `api.playme.ge` (origin) | **Azure App Service Managed Certificate** (DigiCert) | free | West Europe | Bound to the App Service via SNI. CF→origin runs in **Full (strict)** — CF validates the origin hostname against this cert. |
| TLS — `playme.ge` apex + `www` | **Vercel** (Let's Encrypt) | free | edge | Vercel issues automatically against the domain claim (both apex and www). |
| Error monitoring | **Sentry Cloud** | free | EU | One project for web, one for API. API DSN provisioned via `Sentry__Dsn`; empty = SDK disabled (§6.12). |
| Product analytics | **PostHog Cloud** | free | EU | Web + API. Server events require `PostHog__ApiKey`; empty = NoOp client (§6.12). |
| Alerting | **Azure Monitor + email action group** | free | n/a | See §5. |

---

## 3. DNS / TLS

Cloudflare is the authoritative DNS for `playme.ge`. Records currently in the zone:

| Type | Name | Target | Proxy | Purpose |
|---|---|---|---|---|
| CNAME | `api` | `playme-api-prod2.azurewebsites.net` | Proxied (orange) | API. Cloudflare terminates TLS for `api.playme.ge`. |
| A | `playme.ge` (apex) | Vercel edge IPs | Proxied | Canonical web host, served by Vercel. |
| A | `www` | Vercel edge IPs | Proxied | 308-redirects to the apex. |
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

### 6.12 Telemetry secrets fail-closed silently when empty

Both API telemetry env vars are accepted as empty strings and disable the integration with no crash, no warning:

- **`Sentry__Dsn`** — the SDK requires `""` (not `null`) to stay disabled, so `Program.cs:13-15` coalesces a missing config key to empty. Result: no errors, no traces, no boot crash.
- **`PostHog__ApiKey`** — the API's analytics adapter falls back to `NoOpAnalyticsClient` when empty (`AddInfrastructure.cs:74-81`).

Symptom from outside is identical for both: the web SDK reports normally, server-emitted events / errors disappear. We hit it with PostHog when reversi matches weren't appearing in Trends — `match_started` (web) was there, `match_ended` (server) wasn't.

Both are propagated by `infra/provision.sh` from `infra/provision.env` (`SENTRY_DSN`, `POSTHOG_API_KEY`). Keep them populated there — that file is the source of truth per the script header. **A value added in the Azure Portal will be overwritten on the next `provision.sh` run** because `az webapp config appsettings set --settings` deterministically syncs the keys it owns. Restart the App Service after a change so ASP.NET picks up the new config. The `__` (double underscore) in env var names maps to `:` in ASP.NET configuration keys (`Sentry__Dsn` ↔ `Sentry:Dsn`). Historical events from before the var was set are unrecoverable.

### 6.13 Subscription migration (2026-06-10) — what bit us moving tenants

We moved the whole Azure footprint from a personal pay-as-you-go subscription (in an `outlook.com` tenant) to the **"Visual Studio Enterprise Subscription"** in the **JSC BasisBank** tenant (`bb.ge`), for the $150/mo VS Enterprise credit. The API/web hostnames and GHCR image were unchanged; everything else was a fresh `provision.sh` against the new subscription. The traps, in the order they fired:

- **Resource providers start unregistered.** A brand-new subscription has `Microsoft.Web`, `Microsoft.Cache`, `Microsoft.Insights`, `Microsoft.ManagedIdentity`, `Microsoft.OperationalInsights` all `NotRegistered`. Register them (`az provider register -n <ns>`) before provisioning or the creates fail with `MissingSubscriptionRegistration`.
- **Corporate tenant gates Microsoft Graph behind MFA.** ARM (resource) calls work on a normal token, but the AAD/OIDC steps (`az ad app create`, etc.) hit `AADSTS50076` until you re-auth with `az login --tenant <id> --scope https://graph.microsoft.com//.default` and clear MFA. The federated credential GitHub Actions uses is a *workload* identity, so CI deploys are **not** subject to this user-MFA. Also confirm the tenant allows app registration (`authorizationPolicy.defaultUserRolePermissions.allowedToCreateApps`); a bank tenant may not.
- **Global names are reserved across tenants after deletion — and you can't self-serve a release.** App Service soft-deletes a deleted web app for ~30 days (no purge command), so `playme-api-prod` stayed `AlreadyExists`. Azure Cache for Redis rejects re-creating a just-deleted name in a *different* tenant outright ("Name unavailable for reservation"). We renamed to **`playme-api-prod2`** and **`playme-prod-redis2`** rather than wait or open a support ticket. The web app name is only referenced in the `api` CNAME target and the GitHub `AZURE_WEBAPP` var; the Redis name only in the derived connection string.
- **App Service Managed Certificate requires the custom domain to resolve *directly* to the App Service.** With `api.playme.ge` proxied through Cloudflare (orange), Azure's eligibility check sees Cloudflare IPs and refuses ("Hostname not eligible … ensure an active CNAME set to `<app>.azurewebsites.net`"). Temporarily set the `api` CNAME to **DNS-only (grey)**, issue + SNI-bind the cert, then flip back to **Proxied (orange)** and confirm CF SSL mode is **Full (strict)**. This is the mechanic behind §6.1's finickiness.
- **`az webapp wait --created` is gone from the CLI** (≥ 2.84.0 errors `'wait' is misspelled or not recognized`). `provision.sh` now polls `az webapp show` instead.
- **Portal-created managed certs don't show in `az webapp config ssl list`** (they're `Microsoft.Web/certificates` resources). `provision.sh`'s cert step keys off that list, so on a re-run it may try to recreate an already-bound cert — find it with `az resource list --resource-type Microsoft.Web/certificates`.
- **Vercel's server-side `PLAYME_API_URL` was pinned to the old direct origin** (`playme-api-prod.azurewebsites.net`, bypassing Cloudflare for SSR). The rename broke every SSR data-fetch with `getaddrinfo ENOTFOUND` while the client path (`NEXT_PUBLIC_API_URL` → `api.playme.ge`) kept working — homepage fine, room/SSR pages 500. Update `PLAYME_API_URL` in Vercel and **redeploy** (it's read at build time). Pointing it at `https://api.playme.ge` instead of the direct origin decouples SSR from the Azure hostname and prevents a repeat.

---

## 7. Known follow-ups

These aren't blocking launch but each is on the list:

- ~~**Persist Data Protection keys to Redis.**~~ Done. `Microsoft.AspNetCore.DataProtection.StackExchangeRedis` is wired in `AddApi.cs` against the shared `IConnectionMultiplexer`; keys live at `playme:dp-keys` in the same Redis we use for state + the SignalR backplane. `SetApplicationName("playme-api")` namespaces them. Session cookies survive container restarts/redeploys; the key ring is also implicitly shared if we ever horizontally scale.
- ~~**Re-create the Sensitive-flagged Vercel env vars.**~~ Done — `NEXT_PUBLIC_SENTRY_DSN`, `NEXT_PUBLIC_POSTHOG_KEY`, `NEXT_PUBLIC_POSTHOG_HOST` are now inlined into the client bundle.
- ~~**Revisit Azure App Service Managed Certificate.**~~ Done on 2026-05-18 — `.ge` provisioning now works, cert is bound to `api.playme.ge` via SNI, CF→origin upgraded to **Full (strict)**. CF stays in front for WebSocket proxying (§6.2) and Tbilisi-POP latency, not for TLS termination. Path to dropping CF entirely is now clean if we ever want it (see §6.1).
- **Move on-call channel beyond email** when a team forms. See [`security.md`](security.md) §11 / [`roadmap/open-questions.md`](roadmap/open-questions.md).
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
