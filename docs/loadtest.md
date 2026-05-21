# Load test

Basic concurrent-rooms load test for the PlayMe API and the SignalR + Redis path. Closes the last Sprint 7 item from [`roadmap.md`](roadmap.md): _"verify the API and Redis hold up."_

The script lives in [`infra/loadtest/`](../infra/loadtest/) as a standalone `@playme/loadtest` workspace package — runnable but explicitly **not** part of the production build pipeline.

---

## 1. What it does

For each requested room, it runs an independent scenario end-to-end:

1. **Host (HTTP)** — `POST /api/rooms` with `tictactoe-3x3`. Stores the issued session cookie.
2. **Challenger (HTTP)** — `POST /api/rooms/{code}/join` with a separate cookie jar.
3. **Both clients (SignalR)** — open WebSocket connections to `/hubs/room`, attaching their respective `Cookie` headers; then invoke the hub's `JoinRoom(code)` method. Whichever lands second drives the `WaitingForOpponent → InProgress` server transition and triggers a `MatchStarted` broadcast.
4. **Play** — both clients subscribe to `MoveAccepted` and `MatchEnded`. Whoever is the `currentMatch.clock.activePlayer` submits a random unoccupied cell. Loop until `MatchEnded` lands. Moves are spaced ~150ms apart, well below the 60-moves/min per-session limit.
5. **Cleanup** — `hub.stop()` on both connections.

The orchestrator paces new room starts at `--ramp-per-min` (default 5) so a single test machine respects the per-IP rate limits (10 create/min, 5 join/min). Once a room is up, its game runs concurrently with every other open room.

## 2. What it exercises

| Code path | Where it lives | What we're checking |
|---|---|---|
| HTTP rate limit + lock | `RoomsController` + `RedisRoomRepository.WithLockAsync` | Lock-take latency at low contention |
| SignalR backplane | `Microsoft.AspNetCore.SignalR.StackExchangeRedis` | Fan-out of `MoveAccepted` / `MatchEnded` across connections under load |
| Per-session rate limit | `IRateLimiter` / `RedisRateLimiter` | Move flood absorption |
| Game module dispatch | `IGameModule.ApplyMove` | Steady-state move-pipeline throughput |
| Timeout sweeper sizing | `RedisTimeoutSweeperService` | Won't fire (move pace is comfortable), but the schedule churns ZADDs |
| Expiry sweeper | `RedisRoomExpirySweeperService` | Cancellation path runs on every `WaitingForOpponent → InProgress` transition |

What it does **not** exercise: reconnect/grace flow, rematch, resign, the timeout adjudication path (moves are paced fast enough to never time out).

## 3. Prereqs

- Node 22+ (workspace requirement).
- Docker — for the Redis container.
- .NET 10 SDK — for the API.
- `pnpm install` at the repo root (the loadtest package is in `pnpm-workspace.yaml`).

## 4. Running it

In three terminals:

```bash
# Terminal 1 — Redis
docker compose -f infra/docker-compose.yml up redis

# Terminal 2 — API (binds http://localhost:5080 per launchSettings.json)
dotnet run --project apps/api/src/PlayMe.Api

# Terminal 3 — load test
pnpm --filter @playme/loadtest start
```

The default command equates to:

```bash
pnpm --filter @playme/loadtest start -- \
  --target http://localhost:5000 \
  --rooms 50 \
  --ramp-per-min 4
```

A 50-room run takes ~12-13 min of wall-clock at the default 4/min ramp (one room per 15s). The 5/min ramp sits exactly on the join rate limit's fixed-window boundary and reliably trips 429s — only bump it higher if you've widened the limits server-side. For a quick mechanical smoke during iteration, run 5 rooms:

```bash
pnpm --filter @playme/loadtest start -- --rooms 5 --ramp-per-min 4
```

## 5. Interpreting the output

```
────── Load test summary ──────
Target:          http://localhost:5000
Rooms requested: 50  (ramp 5/min)
Rooms completed: 50 ok, 0 failed
Total moves:     312
Wall clock:      10.2min

Per-operation latency:
  createRoom (HTTP)       n=  50  p50=  12ms  p95=  31ms  p99=  82ms  max= 134ms
  joinRoom (HTTP)         n=  50  p50=  10ms  p95=  24ms  p99=  61ms  max=  99ms
  signalR start           n= 100  p50=  41ms  p95=  88ms  p99= 122ms  max= 198ms
  signalR JoinRoom        n= 100  p50=  18ms  p95=  43ms  p99=  78ms  max= 112ms
  submitMove              n= 312  p50=  11ms  p95=  28ms  p99=  54ms  max=  91ms

No errors recorded.
───────────────────────────────
```

**Pass criteria** for the Sprint 7 closeout (local-dev, 50 rooms):

- `Rooms completed: N ok, 0 failed` — every scenario reached `MatchEnded`.
- `submitMove p95 < 100ms` — under low contention, this is dominated by the room-lock take + Redis round-trip; anything higher suggests lock contention or Redis saturation.
- `signalR start p95 < 250ms` — connect + negotiate + WS upgrade. Higher = WebSocket negotiation pressure on Kestrel.
- `signalR JoinRoom p95 < 100ms` — same shape as `submitMove`; both go through `WithLockAsync`.
- No clusters of error-string entries pointing at a single repeated failure.

`429 errors.rate.exceeded` in the error table means the ramp-per-min is set too aggressively for the per-IP limits — drop it back to 5.

## 6. Running against staging / production

**Don't, unless intentionally.** Two reasons:

1. The script's per-IP rate-limit assumption is built for local dev. Against `api.playme.ge` you'll hit Cloudflare WAF / Vercel edge before the API rate limiter triggers.
2. Real load events should be deliberate. If you're sizing the App Service tier, provision a separate staging slot + Redis instance, point `--target` at it, and treat the run as a capacity-planning exercise (see `infra/provision.sh` for the resource shape).

## 7. Result captured (Sprint 7 closeout)

| Date | Target | Rooms | Ramp | Wall clock | Outcome | p95 submitMove | p95 signalR JoinRoom | Notes |
|---|---|---:|---:|---:|---|---:|---:|---|
| 2026-05-20 | local-dev (M1 MacBook Pro, Docker Redis, Debug API) | 10 | 4/min | 136.4s | 10/10 ok, **0 errors**, 83 moves | 14ms | 33ms | First green capture. All latencies well under the runbook thresholds; `submitMove p99` at 19ms. |
