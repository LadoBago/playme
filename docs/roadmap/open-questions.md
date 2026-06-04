# Open questions / deferred to v2

Intentionally unresolved — raise in PRs rather than choosing silently.

- **Native mobile app** (React Native + Expo). Deferred to v2. `packages/shared` is already structured to be consumed by RN when added.
- **Monetization.** No monetization in v1. When introduced, the likely path is rewarded video ads first, then cosmetic IAP — both of which will require introducing optional accounts.
- **Accounts & player stats.** Not in v1 (pure anonymous play). Will become necessary when monetization, leaderboards, friends, or persistent history land.
- **Spectator mode.** Dropped from v1. Revisit after the core 1v1 flow is solid.
- **More games.** Catalog currently ships four modules (`tictactoe`, `connect4`, `reversi`, `seabattle`). Net-new games remain the default — own state shape, own move payload, own win/draw detection, own renderer. **Per-game configurable variants** (board size, ruleset toggles) now go through `gameOptions` on the existing module rather than spawning sibling gameIds — the `tictactoe` consolidation in Sprint 9 set that pattern. Reach for a new module when the rules genuinely diverge; reach for a `gameOptions` knob when only a parameter does.
- **Push notifications.** Web push only (where supported) when re-engagement becomes a priority. Native push waits for the mobile app.
- **Tournaments / prizes.** Not in v1. If pursued later, legal review is required (Georgian gambling-law implications even for skill-based paid entry).
- **Managed log/trace backend.** OTel currently exports to stdout/file. Wire to Grafana Cloud / Honeycomb / similar when scaling beyond one API instance.
- **Secrets vault.** Currently env vars on App Service / Vercel. Once secret count or rotation frequency justifies it, move the API to Azure Key Vault (managed identity → API → Key Vault). Until then, env vars are acceptable.
- **WAF / DDoS.** No dedicated WAF in v1. Cloudflare already fronts `api.playme.ge` (for TLS reasons — see [`deployment.md`](../deployment.md) §6.1), which gives us free baseline DDoS protection on the API path. Vercel fronts the web. If abuse traffic shows up beyond what those handle, escalate to Cloudflare WAF rules or Azure Front Door and re-evaluate rate-limit thresholds in [`security.md`](../security.md) §5.
- ~~**On-call channel.**~~ Resolved: email. Sentry and Azure Monitor both route to the address configured in `infra/provision.env` (`ALERT_EMAIL`). Documented in [`security.md`](../security.md) §11. Revisit if/when a team forms — Slack or a paging service makes more sense above one operator.

When a decision is made, update the relevant doc in the same PR.
