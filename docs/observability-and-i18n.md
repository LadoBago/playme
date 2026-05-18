# Observability and i18n

## 1. Observability

### 1.1 Errors — Sentry

- Frontend (`apps/web`) wires Sentry via `@sentry/nextjs`. Source maps uploaded on deploy.
- Backend (`apps/api`) wires Sentry via `Sentry.AspNetCore`. Releases tagged with the deployed commit SHA.
- Sentry retention is platform-controlled (Sentry free tier ≈ 30 days). Don't try to configure it from code.

### 1.2 Product analytics — PostHog

- Anonymous, cookie-based event tracking. No PII.
- Baseline event set:
  - `room_created`, `room_joined`, `room_expired` — *pending (Sprint 7)*
  - `match_started`, `move_made` — *pending (Sprint 7)*
  - `match_ended` (with `reason`: `win` | `draw` | `resign` | `timeout` | `disconnect`) — **wired in Sprint 5** (fires from `room-client`'s `onMatchEnded` subscription; the `reason` discriminant comes from `outcome.kind`).
  - `rematch_offered`, `rematch_accepted`, `rematch_rejected` — **wired in Sprint 5** (fires from the action sites in `room-client`, not from broadcast subscriptions, so each user action counts once).
- Events fire from the **web client** for user-facing actions and from the **API** for authoritative outcomes (match end, room expiry). The API uses PostHog's .NET SDK; events tagged `source: server` vs `source: web`. The Sprint 5 events all currently fire from the web; moving the authoritative match-end / room-expiry events to the API server-side is a Sprint 7 sub-task.

### 1.3 Logging — Serilog

- API uses **Serilog** with structured logging.
- **7-day rolling file sink** (`logs/playme-.log`, `rollingInterval: Day`, `retainedFileCountLimit: 7`). Older files are pruned automatically.
- Always inject `ILogger<T>`; use structured templates, never string interpolation:

```csharp
_logger.LogInformation("Move accepted in room {RoomId} by {PlayerRole}", roomId, role);
```

- **Never log secrets, invite tokens, or display names** at `Information` level or above. Display names go in PostHog events (anonymous) but not in error logs.

### 1.4 Tracing — OpenTelemetry

- API emits OTel traces and metrics via the `OpenTelemetry.Extensions.Hosting` packages.
- v1 ships traces to **stdout / file** only. A managed backend (Grafana Cloud, Honeycomb, etc.) is **deferred** until we scale beyond one API instance.
- Application Insights is **not** used in v1 (Sentry + PostHog cover the same ground at $0).

---

## 2. Localization (i18n)

- Two locales at launch: **Georgian (`ka`)** and **English (`en`)**. `ka` is the default; `en` is the fallback.
- Web uses **`i18next` + `react-i18next`**. Catalogs are loaded from `packages/shared/i18n/<locale>.json`.
- **Never hard-code user-facing text.** Every visible string must go through a translation key. This includes error messages, button labels, toast text, meta tags, OG titles, and PWA manifest names.
- Backend returns **localized error codes** (e.g. `errors.room.expired`), not localized strings. The client maps codes to translations.
- When introducing new UI text, add the key to **both** `ka.json` and `en.json` in the same PR. Missing translations should fall back to `en`, not show a raw key.

### 2.1 Error code naming convention

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
