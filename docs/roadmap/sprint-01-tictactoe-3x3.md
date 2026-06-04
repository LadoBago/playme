# Sprint 1 — Tic-Tac-Toe 3×3, end-to-end (~1–2 weeks)

First real vertical slice. **No clock, no rematch, no reconnect.** Create-room → join → play → win/draw.

- Domain: `Room`, `Match`, `Move`, `Outcome`, the `tictactoe-3x3` module with full rules.
- Application: `CreateRoomHandler`, `JoinRoomHandler`, `SubmitMoveHandler`, FluentValidation validators, ports (`IRoomRepository`, `IRoomCodeGenerator`).
- Infrastructure: `RedisRoomRepository` using the **Redis distributed-lock pattern from [`state.md`](../state.md) §1** (`LockTakeAsync` / `LockReleaseAsync` on `playme:room:{code}:lock`, 5 s TTL, ~500 ms acquire budget). `RoomCodeGenerator` (cryptographic RNG per [`security.md`](../security.md) §4).
- API: `RoomsController` (POST `/api/rooms`, GET `/api/rooms/{code}`), `RoomHub` at `/hubs/room` (per [`architecture.md`](../architecture.md) §3) with `JoinRoom` and `SubmitMove` methods.
- Web: landing card grid (one card) **with the "How PlayMe works" section already included**, configure page **with a rules tab/panel**, room/match page with the board UI (including the **last-move highlight**) and a "share link" button.
- Generated API client wired (`pnpm gen:api`).
- Server-authoritative validation + win detection. `MatchEnded` includes the winning-line coordinates.

**Exit criteria:** Two browser tabs play a full game from a shared link; illegal moves are rejected with a clear error; the server is the rules authority.
