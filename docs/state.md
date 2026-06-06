# State: Redis schema, room lifecycle, clock model

Authoritative spec for how PlayMe's runtime state is stored, mutated, and transitioned. For the platform rules that produce these transitions, see [`platform.md`](platform.md). For the Hub methods that drive them, see [`architecture.md`](architecture.md) §3.3.

---

## 1. Redis key schema

All keys are prefixed `playme:` to namespace the application. Use `:` as the segment separator — Redis tooling renders it as a tree.

| Pattern | Purpose | TTL |
|---|---|---|
| `playme:room:{roomCode}` | Full room state (players including `hostPlayerId` / `challengerPlayerId` (see [`platform.md`](platform.md) §4), `hostSide` / `challengerSide`, display names, status, current match (incl. setup-commit flags for setup games), clock snapshot, last-tick timestamp, series scoreboard for rematches). Stored as a JSON string. | 30 min while `WaitingForOpponent`, 1 h while `SettingUp` / `InProgress` (refreshed on every interaction), 5 min after `Ended`. |
| `playme:room:{roomCode}:lock` | Distributed lock for atomic move processing (prevents racing `SubmitMove` calls). | ≤ 5 s (auto-expires; held only for the duration of a single move) |
| `playme:rate:{policy}:{key}` | Rate-limit counters (e.g. `playme:rate:move:{playerId}`, `playme:rate:join-code:{roomCode}`). | matches the rate-limit window |
| `playme:timeouts` | Sorted set of scheduled clock-timeout checks. Score = unix-ms deadline, value = `roomCode`. Swept by a `BackgroundService` (see §2 Clock model). | entries are removed by the sweeper after firing; stale entries expire when the room is `Closed`/`Expired` and the sweeper drops them |
| `playme:grace` | Sorted set of scheduled disconnect-grace deadlines. Score = unix-ms deadline, value = `{roomCode}:{role}`. Swept by a `BackgroundService` (Sprint 5). | removed by the sweeper or on reconnect |
| `playme:expires` | Sorted set of scheduled `WaitingForOpponent` expiry deadlines — fires `room_expired` analytics + the `RoomExpired` SignalR event for rooms nobody joined. Score = unix-ms deadline, value = <code>{roomCode}&#124;{gameId}</code> (gameId rides on the member because the room key has typically already elapsed when the sweeper fires). Enrolled by `CreateRoomHandler`; ZREM'd by `RegisterPresenceHandler` when the room leaves `WaitingForOpponent`. | removed by the sweeper after adjudication or on join |
| `playme:setup_deadlines` | Sorted set of scheduled setup-phase deadlines (Sprint 10 seam C). Score = unix-ms deadline, value = `roomCode`. One entry per `SettingUp` room — enrolled at SettingUp entry (deadline = entry + the module's `SetupBudget`), ZREM'd when setup completes or the match ends during setup. On fire: one uncommitted side → forfeits with `Outcome.Timeout`; neither committed → room `Expired`. | removed by the sweeper after adjudication or on setup completion |
| `playme:signalr:*` | SignalR backplane channels managed by `Microsoft.AspNetCore.SignalR.StackExchangeRedis`. **Don't read or write these manually.** | managed by the library |

Implementation rules:

- **State shape — single JSON blob per room, not decomposed.** The room key holds the entire `Room` aggregate (players, status, current match, clock fields, scoreboard, last-tick timestamp) serialized as one JSON document by `System.Text.Json`. **Do not split** into a Redis hash, multiple keys, or sub-documents. Rationale: writes are bounded by move rate (the clock model in §2 is lazy — state is only mutated on real events, not periodically), the document is small (~1–5 KB), one `GET` returns a consistent snapshot, and the C# `Room` aggregate maps 1:1 to the document so Infrastructure doesn't dictate Domain shape. **Exception:** append-only move history (for future replay support) lives in a separate `playme:room:{code}:moves` Redis list — additive, doesn't affect the main state schema.
- **Atomic move processing — Redis distributed lock per room.** Acquire `playme:room:{roomCode}:lock` via StackExchange.Redis `IDatabase.LockTakeAsync` (5 s TTL, library-generated unique token). Inside the lock: read room state → validate the move with the C# rules engine in `Domain` → write new state → call `LockReleaseAsync` (its release uses a small CAS Lua internally, so a lock is only released by the holder). Bound the acquire wait to ~500 ms; on timeout, reject the move with an `ErrorCode.Busy`-style code and let the client retry. **No application-level Lua scripts. No `WATCH/MULTI/EXEC` retry loops.** Rules logic stays in `Domain`/`Application` where it's testable and unique; the lock provides cross-instance mutual exclusion without duplicating it elsewhere. Contention is bounded by turn-based play (only the active player can legitimately move), so the lock is virtually uncontended in practice.
- **In-process C# locks are insufficient.** The API runs multi-instance behind a load balancer; `lock(obj)` only coordinates within one process. Any per-room critical section must use the Redis-distributed lock above. This earlier "don't use application-level locks" advice was specifically about in-process locks — Redis-distributed locks are the correct primitive.
- **TTL refresh on activity.** Every `Application` handler that reads or writes the room must refresh its TTL at the end (`EXPIRE` or `SET ... EX`). Idle rooms expire on their own.
- **Never store secrets, session tokens, or display names beyond the room state.** Cleanup happens via TTL, not by application code.
- **Don't enumerate keys by pattern (`KEYS playme:*`).** It's O(N) over the whole keyspace and blocks Redis. If enumeration becomes necessary, add a secondary index set (`playme:rooms:active`).

---

## 2. Room lifecycle / state machine

Every room follows a small finite-state machine, enforced server-side. Handlers reject transitions not in this table.

```
            ┌──────────────────────┐    TTL elapsed     ┌─────────────┐
            │  WaitingForOpponent  │───────────────────▶│   Expired   │  (terminal)
            └──────────┬───────────┘                    └─────▲───────┘
   both registered     │                                      │
   + both connected    │                                      │ setup deadline,
            ┌──────────▼───────────┐  setup games only        │ neither committed
            │      SettingUp       │──────────────────────────┘
            │ (ISetupGame modules; └────────────┐
            │  setup-less games    │            │ setup deadline w/ one committed →
            │  skip this state)    │            │ uncommitted side forfeits (Timeout);
            └──────────┬───────────┘            │ disconnect-grace elapse → Disconnect
   both setups         │                        │
   committed           │                        │
            ┌──────────▼───────────┐            │
            │      InProgress      │            │
            └──────────┬───────────┘            │
                       │ win / draw / resign / timeout
            ┌──────────▼───────────┐            │
            │        Ended         │◀───────────┘
            └──────────┬───────────┘
        rematch offered │       │ either player exits
            ┌──────────▼─────┐   │
            │ AwaitingRematch│   │
            └─┬──────────┬───┘   │
       accept │          │ reject │
              │          ▼        ▼
              │      ┌─────────────────┐
              │      │     Closed      │  (terminal)
              │      └─────────────────┘
              └──→ loops back to InProgress for a new Match
                   (setup games loop back through SettingUp — fresh setup
                    every match, sides already swapped)
```

### 2.1 States

- **`WaitingForOpponent`** — the initial state after room creation. **Governed purely by the room TTL** (default 30 min from creation, refreshed on challenger registration). Player disconnects in this state are **transparent**: host or challenger may close their tab and return at any time before the TTL elapses — the session cookie ties them to their role. Challenger registration (completing the join-onboarding form per [`frontend.md`](frontend.md) §1) consumes the invite link; the seat is sticky and no one else can take it. **Transition to `InProgress` requires both: (1) both players have completed registration, AND (2) both currently have an active SignalR connection in the room.** Until both conditions hold, the room stays in `WaitingForOpponent`. If the TTL elapses before that, the room goes terminal to `Expired`.
- **`SettingUp`** *(setup games only — Sprint 10 seam C)* — both players are present; each privately prepares and commits their one-and-final setup payload (`SubmitSetup`). **Unclocked**: the chess clock does not run; the phase is bounded by the module's `SetupBudget` via `playme:setup_deadlines`. Presence is tracked like `InProgress` — a disconnect by a player who hasn't committed schedules a disconnect-grace entry (started immediately; setup has no turns) and grace elapse forfeits them with `Outcome.Disconnect`; a player who already committed owes nothing until the match starts. The match aggregate exists from SettingUp entry (it accumulates the setup state); for hidden-state games every wire payload is role-projected per §2.3. Transition to `InProgress` when the module reports setup complete; the clock is re-stamped so the first mover starts from the full budget.
- **`InProgress`** — both players joined (and, for setup games, both setups committed); a match is being played. Clocks tick. The first-mover's clock starts immediately on entry (platform rule [`platform.md`](platform.md) §1 #12).
- **`Ended`** — the current match has concluded. Post-match UI is shown. Either player can offer a rematch or exit.
- **`AwaitingRematch`** — one player offered a rematch; waiting for the opponent's accept/reject.
- **`Closed`** *(terminal)* — the room is cleaned up; both invite links are dead. Reached when (a) anyone exits without a rematch, (b) a rematch is rejected, (c) the post-`Ended` cleanup TTL elapses.
- **`Expired`** *(terminal)* — reached only from `WaitingForOpponent` when nobody joined within the TTL.

### 2.2 Clock model (server-authoritative, lazy state)

The clock state lives in the Redis room hash: `lastTickAt` (server UTC ms), `activePlayer` (`host` | `challenger`), `hostClockMs`, `challengerClockMs`. Stored values represent remaining time *as of `lastTickAt`*. **No background timer mutates clock state every second.** The effective clock at moment `T` is computed lazily — for the active player: `storedClockMs - (T - lastTickAt)`; for the inactive player: the stored value unchanged. State is rewritten only when something changes (move accepted, match ends, room closes).

Clients extrapolate locally between server snapshots (`displayedMs = serverClockAtSnapshot - (Date.now() - snapshotReceivedAt)`) and re-sync whenever a fresh snapshot arrives. Every state-mutating server event (`MatchStarted`, `MoveAccepted`, `MatchEnded`, `OpponentDisconnected`, `OpponentReconnected`, presence responses) carries a `ClockSnapshotDto`; **no separate periodic `ClockTick` event is broadcast.** The web client re-renders the countdown locally at ~10 Hz (`apps/web/app/r/[code]/clock.tsx`) using the most recent snapshot. Drift is bounded by network RTT and irrelevant at 1-second display granularity. The `ClockTick` event name is **reserved** (see §2.3) for a possible future drift-correction sweep; don't add a periodic broadcast without a concrete drift symptom to fix.

**Timeout detection** is two-pronged:

1. *At move time* — the move handler recomputes the active player's effective clock *before* validating the move. If it's `≤ 0`, emit `MatchEnded(Outcome.Timeout)` instead of accepting the move.
2. *No-move timeout* — when a room enters `InProgress` or accepts a move, schedule one delayed timeout check at `lastTickAt + activePlayerRemainingMs`. Implementation: a Redis sorted set (`playme:timeouts`, score = unix-ms deadline, value = `roomCode`), swept by a single `BackgroundService` per API instance every ~250 ms via `ZRANGEBYSCORE ... LIMIT 0 N`. **The sweeper MUST acquire the room lock (`playme:room:{roomCode}:lock` per §1) before processing each expired entry** — this serializes timeout processing against concurrent move handlers and against other API instances' sweepers, preventing duplicate `MatchEnded` emissions. The processing sequence for each expired entry: (1) attempt `LockTakeAsync` with a short acquire wait (~100 ms); on lock-contention, skip — the next sweep will retry. (2) Inside the lock, re-read the room state. (3) If the room is still `InProgress`, `lastTickAt` hasn't advanced, the active player is unchanged, and effective clock is ≤ 0, emit `MatchEnded(Outcome.Timeout)` and transition the room to `Ended`. Otherwise drop silently (a move happened, a new check was scheduled). (4) `ZREM` the entry regardless of outcome — the entry has been adjudicated. (5) Release the lock. Dead-instance safety: if the sweeper crashes mid-processing, the lock's 5 s TTL releases it automatically; the `ZREM` never happens, so the entry stays in the set and the next sweep retries.

This pattern means **one scheduled task per active room — not a per-room 1-second timer.** It matches the approach Lichess and Chess.com use for authoritative chess clocks at scale.

### 2.3 Server-emitted events

Broadcast to both clients via SignalR unless noted. **Hidden-state games** (modules implementing `IHiddenStateGame`, Sprint 10 seam A): while the match has no outcome, events that carry a room payload are delivered to the per-role groups `room:{code}:host` / `room:{code}:challenger` — one per-viewer projection each — instead of one payload to the room group; once the match is terminal, delivery and payload revert to the shared full state. Perfect-information games are unaffected.

| Event | When | Payload |
|---|---|---|
| `OpponentJoined` | challenger join | challenger display name, side/color |
| `MatchStarted` | entering `InProgress` (initial, after rematch accept, or — for setup games — on the setup-completing commit) | starting clock snapshot, both players' sides (**swapped on each rematch** per [`platform.md`](platform.md) §1 #15), who moves first |
| `SetupStarted` | entering `SettingUp` (setup games only; initial or after rematch accept) | room snapshot (role-projected for hidden-state games); clients mount the setup screen |
| `OpponentSetupCommitted` | a player's `SubmitSetup` is accepted without completing setup — sent to the other player only | committing role + room snapshot (readiness flags in `match.setup`; never the setup content) |
| `MoveAccepted` | server accepts a `SubmitMove` | move, updated board state, who's next, clock snapshot |
| `MoveRejected` | server rejects a `SubmitMove` | reason code (illegal cell, full column, not-your-turn) — sent to submitter only |
| `ClockTick` *(reserved — not emitted today)* | Event name reserved for a future drift-correction sweep. **Not implemented by design** — every state-mutating event in this table already carries a `ClockSnapshotDto`, and the client interpolates between snapshots locally. Only add a periodic broadcast if a measurable drift problem appears in the wild (see §2.2). | per-player remaining time |
| `MatchEnded` | `InProgress` → `Ended` | outcome, winning-line coordinates (if `Win`), final clock |
| `RematchOffered` | a player offers rematch | which player offered |
| `RematchAccepted` | `AwaitingRematch` → `InProgress` (new match) | (followed by `MatchStarted`) |
| `RematchDeclined` | `AwaitingRematch` → `Closed` | sent to the offerer; the rejector is auto-routed to the lobby |
| `OpponentDisconnected` | a player's SignalR connection drops | sent to the still-connected player |
| ~~`OpponentAbandoned`~~ | *(removed)* The reconnect grace ([`platform.md`](platform.md) §1 #7) is a **hard cutoff**: when it elapses the server auto-ends the match with `Outcome.Disconnect(disconnectedSide)` and emits `MatchEnded` directly — there is no intermediate notice to the still-connected player. |
| `OpponentReconnected` | dropped player rejoins (before *or after* grace, while match is still `InProgress`) | sent to the still-connected player |
| `OpponentExited` | a player leaves the room while in `Ended` or `AwaitingRematch` — either via an explicit `ExitRoom()` call (immediate) or via a SignalR disconnect that doesn't reconnect within the post-match reconnect grace (10 s, see §2.4 invariants) | sent to the still-present player; their UI shows "opponent left" + a manual "Back to lobby" button. Room transitions to `Closed`. |
| `RoomExpired` | room reaches `Expired` or post-`Ended` cleanup TTL | `reason`: `unjoined` (the `WaitingForOpponent` 30-min window elapsed with no challenger) or `setupTimeout` (a setup game's deadline elapsed with neither side committed — a one-sided miss is a forfeit / `MatchEnded` instead) |

### 2.4 Invariants

- All transitions are server-driven. Clients can *request* transitions (`SubmitMove`, `OfferRematch`) but cannot *perform* them.
- A move is only accepted in `InProgress`.
- A rematch offer is only accepted in `Ended` and transitions to `AwaitingRematch`.
- The clock keeps running during `OpponentDisconnected` (platform rule [`platform.md`](platform.md) §1 #7).
- The reconnect grace ([`platform.md`](platform.md) §1 #7) is a server-side hard cutoff: when it elapses, `AdjudicateDisconnectGraceHandler` ends the match with `Outcome.Disconnect(disconnectedSide)`. No client-driven `ClaimVictory` affordance exists.
- `ExitRoom()` is valid only in `Ended` or `AwaitingRematch`. It transitions the room directly to `Closed` and emits `OpponentExited` to the still-present player.
- **A SignalR disconnect from `Ended` or `AwaitingRematch` is NOT treated as an immediate exit.** Refresh, locale toggle, route change, and brief network blips all manifest as a disconnect at the SignalR layer; treating them as exits would tear the room down on legitimate UX flows. Instead, `ReleasePresenceHandler` marks the role disconnected and schedules a 10 s post-match reconnect grace (`playme:postmatch_exit` sorted set). If the player reconnects in time, `RegisterPresenceHandler` cancels the entry and the still-present player sees nothing. If the grace elapses without a reconnect, the post-match-exit sweeper dispatches `AdjudicatePostMatchExitGraceHandler`, which transitions the room to `Closed`; the sweeper then broadcasts `OpponentExited` to the room group via `IRoomNotifier.BroadcastOpponentExitedAsync`. The defensive precondition re-check (status still post-match, role still disconnected) covers the race where the player reconnects in parallel with the sweeper. Different from the `InProgress` reconnect grace ([`platform.md`](platform.md) §1 #7), which adjudicates a match outcome — this one only handles the room exit.
- Once `Closed`, the room is non-joinable, all subsequent Hub calls return `ErrorCode.RoomClosed`, and the post-`Ended` TTL (5 min per §1) eventually deletes the Redis state.
