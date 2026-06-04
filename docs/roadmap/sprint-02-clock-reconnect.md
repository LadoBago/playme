# Sprint 2 — Chess clock + reconnect (~1 week)

- `IClock` in `Application/Abstractions`; `SystemClock` in `Infrastructure`.
- Server-side **lazy** clock (no background per-room timer). Each state-mutating event (`MatchStarted`, `MoveAccepted`, `MatchEnded`, `OpponentDisconnected`/`Reconnected`, presence responses) carries a `ClockSnapshotDto`. The `ClockTick` event name is **reserved but not emitted** — the snapshot rides on existing events and the client interpolates locally. See [`state.md`](../state.md) §2.2.
- Match ends on timeout → `MatchEnded` with `Outcome.Timeout`.
- Client renders the countdown by extrapolating the last snapshot locally — no client-side free-run, no client-authoritative timing.
- SignalR reconnect with a 30s grace window. Clock keeps running through disconnect. New events: `OpponentDisconnected`, `OpponentReconnected`.

**Exit criteria:** A game can time out; a player can close and reopen a tab within 30s without losing state.
