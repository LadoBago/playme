# Load test

Load test for the PlayMe API and the SignalR + Redis path. Originally closed the Sprint 7 item from [`roadmap/sprint-07-hardening.md`](roadmap/sprint-07-hardening.md) (_"verify the API and Redis hold up"_); the **sustained** mode (§8) extends it into a pre-launch capacity test against production.

The script lives in [`infra/loadtest/`](../infra/loadtest/) as a standalone `@playme/loadtest` workspace package — runnable but explicitly **not** part of the production build pipeline.

It has two modes, selected with `--mode`:

- **`burst`** (default, §1–§5) — spawns N independent one-shot games, each played end-to-end as fast as the move limiter allows. Mechanically exercises the whole pipeline; concurrency stays near zero. This is the Sprint 7 scenario.
- **`sustained`** (§8) — ramps long-lived match **pairs** (two players who keep playing rematches at human pace) through a series of concurrency steps, reporting per-step latency. This is the shape real load has, and the mode to use for capacity sizing.

---

## 1. What burst mode does

For each requested room, it runs an independent scenario end-to-end:

1. **Host (HTTP)** — `POST /api/rooms` with `gameId: "tictactoe"` and `gameOptions: { boardSize: 3 }`. Stores the issued session cookie.
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

`429 errors.rate.exceeded` in the error table means the ramp-per-min is set too aggressively for the per-IP limits — drop it back to 5, or widen the limits server-side (see §8.1).

## 6. Running burst mode against staging / production

For mechanical smoke against a deployed environment, point `--target` at it and keep the run small. Two caveats:

1. **Hit the Azure origin directly, not `api.playme.ge`.** High request volume through the Cloudflare proxy violates CF's free-tier terms and CF may challenge/throttle the run, polluting results. The origin hostname is `playme-api-prod.azurewebsites.net` (see [`deployment.md`](deployment.md) §3) — non-browser clients don't care about CORS. Do one *small* pass through `api.playme.ge` afterwards to validate the CF→origin WebSocket path end-to-end.
2. **Widen the per-IP limits first** (next section), or the harness throttles itself before the platform feels anything.

## 7. Result captured (Sprint 7 closeout)

| Date | Target | Rooms | Ramp | Wall clock | Outcome | p95 submitMove | p95 signalR JoinRoom | Notes |
|---|---|---:|---:|---:|---|---:|---:|---|
| 2026-05-20 | local-dev (M1 MacBook Pro, Docker Redis, Debug API) | 10 | 4/min | 136.4s | 10/10 ok, **0 errors**, 83 moves | 14ms | 33ms | First green capture. All latencies well under the runbook thresholds; `submitMove p99` at 19ms. |

## 8. Sustained mode — production capacity test

`--mode sustained` is the pre-launch capacity test: "how many simultaneous matches can the B1 + C0 tiers hold before move latency degrades?" It ramps **match pairs** (each = two players, two WebSocket connections, a steady move stream, and back-to-back rematches at human think-time) through a series of concurrency steps, holds each step, and prints per-step latency.

```bash
pnpm --filter @playme/loadtest start -- \
  --target https://playme-api-prod.azurewebsites.net \
  --mode sustained \
  --steps 10,25,50,100,200 \   # concurrent pairs per step (×2 = connections)
  --hold-sec 300 \             # measurement window per step
  --think-min-ms 1000 --think-max-ms 4000 \   # per-move human delay
  --launch-per-min 30          # pair-launch pace during ramp-up
```

Each step launches pairs up to its target, settles, then measures a clean `--hold-sec` window. `Ctrl-C` drains gracefully. The harness exits non-zero if any pair died mid-run.

### 8.0 How a pair works

A **pair** is one self-driving match between two simulated players that stays alive for the whole run. Unlike a burst-mode scenario (one game, then teardown), a pair plays continuously:

1. **Set up once** — host `POST /api/rooms`, challenger `POST /{code}/join`, both open a SignalR WebSocket and `JoinRoom`. The pair now holds two live connections for the rest of the test.
2. **Drive both sides from one event stream** — the pair listens to the *host* hub's broadcasts (`MatchStarted` / `MoveAccepted` / `MatchEnded` / `RematchOffered`), each carrying the authoritative room snapshot. Consuming a single stream (rather than both hubs') avoids stale cross-connection overwrites. A monotonic `version` counter wakes the driver whenever a new snapshot lands.
3. **Play at human pace** — whichever side the snapshot says is to move waits a random `think-min..think-max` ms, then submits a random free cell on the side's own connection. After submitting, it waits for the next snapshot (keyed on the *pre-submit* version, so an already-delivered `MoveAccepted` doesn't deadlock it).
4. **Rematch and repeat** — on `MatchEnded`, the host `OfferRematch`s and the challenger `AcceptRematch`s (seeded from the accept's own return value to dodge a snapshot race), and the loop continues into the next match. This is what keeps the WebSockets and per-room Redis traffic alive between games.
5. **Stall guard** — at human pace a healthy match never goes silent for more than ~10 s, so six consecutive silent waits (~60 s) means the pair wedged (lost broadcast, server-closed room); it's recorded as a death and torn down rather than hanging the run.

**Concurrency** is just many pairs running this loop at once. N pairs ≈ 2N live connections + a steady move stream — the load shape production actually has. The orchestrator launches pairs up to each step's target at `--launch-per-min`, drains a clean measurement window per step (ramp-phase samples are discarded so the numbers reflect steady state), and tracks `alive` / `deaths` / matches-completed live. A "death" is counted exactly once — a pair that fails during setup (e.g. a `429`) or wedges mid-run — and ramp/setup errors are surfaced in a per-step block before the window drains them, so a setup failure can't hide behind an empty error table.

### 8.1 The rate-limit prerequisite (do this first)

Every pair from one test machine shares one source IP, so the per-IP defaults (`POST /api/rooms` 30/min, `/join` 30/min — see [`security.md`](security.md) §5) throttle the **harness**, not the platform: a 200-pair ramp launched in a burst far exceeds 30 joins/min and most pairs would die on a `429 errors.rate.exceeded` during setup (these surface in a per-step "ramp/setup errors" block). The counts are bindable via `IpRateLimitingOptions`; before a run, widen them on the App Service:

```bash
az webapp config appsettings set -g <rg> -n playme-api-prod --settings \
  RateLimiting__Ip__RoomsCreatePermitLimit=600 \
  RateLimiting__Ip__RoomsJoinPermitLimit=600 \
  RateLimiting__Ip__RoomsGetPermitLimit=600
```

**After the run, delete these overrides** (don't re-set them) so the code defaults reapply — a stale override silently wins over future default changes:

```bash
az webapp config appsettings delete -g <rg> -n playme-api-prod \
  --setting-names RateLimiting__Ip__RoomsCreatePermitLimit \
                  RateLimiting__Ip__RoomsJoinPermitLimit \
                  RateLimiting__Ip__RoomsGetPermitLimit
```

They are abuse controls, not test settings. The per-session move limit (60/min) is deliberately *not* configurable; sustained mode's human think-time stays well under it.

### 8.2 What to watch (three places at once)

| Where | Metric | What a bad number means |
|---|---|---|
| Harness output | `submitMove` p95/p99 per step | The headline. Rising p95 as steps grow = the platform saturating. |
| Harness output | `pairs alive: N/target`, `deaths` | Connections dropping under load (Kestrel/SignalR pressure). |
| Azure portal — App Service | CPU %, memory, WebSocket count | B1 is single-vCPU; CPU pegging at 100% is the likely first wall. |
| Azure portal — Redis | Server Load %, Connected Clients | C0 is shared-infra; Server Load → 100% means Redis is the wall. |
| Sentry | new issues during the run | Watch for the #147 `RedisTimeoutException` reappearing at 15s — that's the thread-pool-starvation signature under load. |

### 8.3 Reading the ramp

Stop escalating steps once `submitMove` p95 crosses ~1 s **or** deaths appear **or** App Service CPU sits at 100%. The last *clean* step is your headroom: _"B1 + C0 holds X simultaneous matches with p95 move RTT under Y ms."_ That number is both the launch-confidence figure and the scale-up trigger (when real concurrent matches approach X, bump the Redis/App Service tier — see [`deployment.md`](deployment.md)).

### 8.4 Results captured

**2026-06-05 — first production capacity run.** Target: Azure origin (`playme-api-prod.azurewebsites.net`), B1 App Service + C0 Redis, West Europe. Ramp `10,25,50,100,200` pairs, 300 s hold each, think 1–4 s, launch 60/min. Driven from a single dev machine (Tbilisi → West Europe), so absolute latencies carry a fixed network tax — read the *trend across steps*, not the raw numbers.

`submitMove` latency by step:

| step | pairs | conns | matches | p50 | p95 | p99 | max | deaths |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 10 | 20 | 126 | 144ms | 803ms | 1.22s | 2.18s | 0 |
| 2 | 25 | 50 | 315 | 157ms | 653ms | 1.02s | 2.03s | 0 |
| 3 | 50 | 100 | 630 | 172ms | 892ms | 1.64s | 3.56s | 0 |
| 4 | 100 | 200 | 1176 | **285ms** | **1.31s** | 2.43s | 3.78s | 0 |
| 5 | 200 | 400 | 1668 | 478ms | 2.43s | 4.25s | 6.13s | **41** |

Totals: 200 pairs launched, **4,543 matches** completed, **41 deaths** — all at the step-5 ramp, from `room.busy` (per-room lock-acquire timeouts under CPU saturation), zero in steps 1–4.

**Findings:**

- **≤ 50 pairs:** flat latency, App Service CPU has headroom, zero errors. Comfortable operating range.
- **100 pairs (step 4):** the knee. p50 nearly doubles (172 → 285 ms) and p95 crosses 1 s — this coincided with the App Service Plan's `CPU Percentage` hitting ~100% (single B1 vCPU). Still 0 deaths: it holds, just slower.
- **200 pairs (step 5):** past the wall. p95 2.4 s, and the server sheds `JoinRoom` with retryable `errors.room.busy` (= `LockTimeoutException`; the CPU-starved server can't take the room lock within its ~100 ms budget). Graceful degradation — no crash, no data loss; a real browser client would retry.
- **Bottleneck is App Service CPU, not Redis.** C0 Server Load stayed comfortable throughout; the single B1 vCPU is the wall.

**Verdict:** B1 + C0 comfortably serves **~50 simultaneous matches**, with **~100 the soft ceiling**. Scale-up trigger: when real concurrent matches approach ~50, bump the App Service plan (more vCPU) before quality degrades — see [`deployment.md`](deployment.md). Ample runway for an anonymous pre-launch casual game.
