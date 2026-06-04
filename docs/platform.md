# Platform layer and domain vocabulary

Every game shares a **platform layer**. Game code never reimplements platform features. Each game is a **self-contained module** for its rules and UI.

For the state machine and Redis schema, see [`state.md`](state.md). For Hub methods, see [`architecture.md`](architecture.md) §3.3. For the canonical per-game rules, see the per-game specs in [`games/`](games/) (§2 below).

---

## 1. Platform layer (shared, one implementation)

1. **Room lifecycle** — creation, single-use invite link, TTL, cleanup.
2. **Two-role model** — host and challenger, exactly two players per room.
3. **Chess clock** — fixed total per player, server-authoritative, ticks server-side. **Three presets only** (no custom values, no "unlimited" in v1): **1 min**, **3 min**, **10 min** per player. Both players always get the same time bank. Modeled as a strongly-typed enum `TimeLimit { OneMin, ThreeMin, TenMin }` on the API; FluentValidation rejects anything else with `errors.config.invalidTimeLimit`. Room state stores the resolved `timeLimitMs` for the clock model in [`state.md`](state.md) §2. **Per-game default budgets** live on each `IGameModule.DefaultClockBudget`: `tictactoe` → 3 min, `connect4` → 3 min, `reversi` → 10 min. All three presets remain selectable for every game — defaults are just the preselected option on the configure page. **Size-driven sub-defaults** for `tictactoe` (1 min for 3×3, 3 min for 6×6, 10 min for 9×9) were considered during Sprint 9 but deferred — the platform has no host-selected clock picker today, so the module's single value applies until that picker lands (see [`roadmap/deferred-polish.md`](roadmap/deferred-polish.md)).
4. **Online move pipeline** — every move travels client → server → opponent via SignalR.
5. **Host-created matches** — host chooses game type, time limit, side/color, display name.
6. **Invite flow** — host shares the link; first non-host to open it becomes the opponent.
7. **Connection-loss tolerance** — applies to the `InProgress` state. **The clock keeps running during disconnect** (no pause). Reconnect within the grace window and the player rejoins the match seamlessly. **The grace is a hard cutoff, not a UX threshold** — when it elapses, the server automatically ends the match with `Outcome.Disconnect(disconnectedSide)` (opponent wins). There is no `ClaimVictory` affordance; the still-connected player just waits the grace out and the server adjudicates. **Grace duration is tiered by the configured clock budget**, so a short bullet game isn't extended by a wait equal to a meaningful fraction of its own clock:

   | Per-side clock budget | Grace window |
   |---|---|
   | ≤ 1 min | **none** — the disconnected player's chess clock will run out first anyway; no separate abandon entry is scheduled |
   | > 1 min, ≤ 5 min | 60 s |
   | > 5 min | 90 s |

   **Two further conditions make the grace timer behave exactly like the chess clock:**

   (a) The grace timer only ticks while it's the **disconnected player's turn**. If the connected player is on the move when the disconnect happens, the grace timer doesn't start; once the connected player makes their move and the turn flips to the (still-disconnected) opponent, *then* the grace starts ticking. This mirrors the lazy chess clock from [`state.md`](state.md) §2.2 — a player is only penalized for being away when the game is actually waiting on them.

   (b) The grace entry is only scheduled if the disconnected player's **effective remaining chess clock** at the scheduling moment is **strictly greater than** the grace window. Otherwise the existing chess-clock timeout (which is already scheduled by the move pipeline) would fire first anyway, ending the match with `Outcome.Timeout(disconnectedSide)`. Avoiding a redundant grace entry keeps the wire honest about cause (timeout vs. abandon) and saves a Redis round-trip.

   **Reconnect cancels the pending grace entry** (already wired in `RegisterPresenceHandler`). A reconnect before the entry fires returns the room to normal play; a reconnect after the entry has fired races against the room lock and loses.

   The still-connected active player can submit moves normally while the opponent is offline — move acceptance gates on `room.Status == InProgress` and "caller is the active player," not on opponent presence. Disconnects during `WaitingForOpponent` follow a different rule ([`state.md`](state.md) §2): they are transparent, governed by the room TTL rather than a short reconnect window, because there is no clock to enforce. Disconnects after `Ended` / `AwaitingRematch` follow `state.md` §2.4 (treated identically to an explicit `ExitRoom`).
8. **Resign** — always behind an explicit confirm step to prevent accidental clicks.
9. **Post-match handling** — winner/loser can offer rematch or exit to lobby.
10. **Rematch handshake** — either player can offer a rematch from the `Ended` state. The server serializes `OfferRematch` calls via the [`state.md`](state.md) §1 room lock, so **only one offer is active at a time**. First offer wins: it transitions `Ended → AwaitingRematch` and records the offerer; the other player's UI then shows **Accept / Reject** (replacing their own "Offer rematch" button). A second `OfferRematch` from the opponent (near-simultaneous clicks) is treated as an **implicit accept** — the room transitions to `InProgress` with swapped sides per §1 #15. `AcceptRematch` from the responder has the same effect. `RejectRematch` is valid **only for the responder** (not the offerer); the rejector auto-returns to the lobby, the offerer stays in the room with a "rematch declined" notice and a manual "Back to lobby" button. Cancelling your own offer is **not** a v1 feature — once offered, the only exits are opponent-accept, opponent-reject, opponent-exit, or the offerer manually exiting (via the "Back to lobby" button, which calls `ExitRoom()` per the invariants in [`state.md`](state.md) §2).
11. **First-move ownership** — determined per game by the canonical rule (X first in Tic-Tac-Toe, **Red first in Connect 4**). The player assigned the first-move side moves first, regardless of how the assignment happened (host's specific choice, server-random, or challenger-picked per #14).
12. **Clock-start rule** — clock for the side that moves first **starts immediately** when both players are present in the room. No "ready up" step.
13. **Rematch-series scoring** — while both players stay in the same room across rematches, the server tracks a session-only scoreboard. **Scoring rule:** Win = 1 point, Draw = 0, Loss = 0 (win-only; the user's chosen rule, not chess-style 1/½/0). **Schema:** `seriesScore: { host: int, challenger: int, draws: int }` — `host` and `challenger` count their wins; `draws` is shared (not per-player) and tracked for display context, not scoring. **Outcome mapping:** `Win` → opponent's loser-side stays unchanged, winner side `+= 1`. `Draw` → `draws += 1`. **`Resign`, `Timeout`, and `Disconnect` roll into the opponent's win** — they're not separate score categories, since from a player's perspective "I won that game" reads the same whether the other side ran out of time, gave up, disconnected, or got beaten on the board. **Display:** primary score line is the win count (`Lado 2 — 1 Nika`); if `draws > 0`, append a small subtitle (`1 draw`). The total matches played is `host + challenger + draws`. The scoreboard is server-authoritative, lives in the room state in Redis, and is discarded when the room reaches `Closed` or `Expired`. No persistence beyond the room.
14. **Side/color selection** — at room creation the host chooses one of **three options** for the side/color split: (a) **specific side** — host picks their own side (X or O for Tic-Tac-Toe; red or yellow for Connect 4); challenger automatically gets the other. (b) **Random** — server picks the host's side at room creation and the challenger gets the other; both players see their assignment as read-only info. (c) **Let challenger pick** — sides remain unresolved until the challenger's join-onboarding step, where they select one of the two available sides and the host gets whichever they don't pick. In all three options, **both sides are fully resolved before the room transitions to `InProgress`**, which keeps platform invariant #12 (clock starts immediately when both players are in the room) intact. The room state stores `hostSide` and `challengerSide`; under option (c) both are `null` until the challenger picks.
15. **Rematch side swap** — on every accepted rematch (transition from `AwaitingRematch` back to `InProgress` for a new match within the same room), the server **swaps `hostSide` and `challengerSide` deterministically**. Whoever had X last match plays O; whoever had red plays yellow. Applies regardless of how sides were originally chosen (#14). **Rationale:** first-move advantage is real (especially on 9×9 Tic-Tac-Toe and in Connect 4); alternating sides across a rematch series makes the per-player scoreboard (#13) reflect playing strength rather than side luck. The swap is automatic — no joiner re-prompt — and the `MatchStarted` event for the new match carries the swapped assignments and who moves first. UI must clearly display each player's *current* side in a persistent HUD slot, since it changes match-to-match within a session.

## 2. Game modules (independent, no shared engine)

| Module | Game | Canonical spec |
|---|---|---|
| `tictactoe` | Tic-Tac-Toe on N×N. `gameOptions: { boardSize ∈ {3, 6, 9} }` picks the grid; win length is derived deterministically (3→3, 6→4, 9→5). Sides: **X** and **O**. | [`games/tictactoe.md`](games/tictactoe.md) |
| `connect4` | Connect 4 on 7×6 with gravity, win = 4 in a row. Colors: **red** and **yellow** (traditional pair). | [`games/connect4.md`](games/connect4.md) |
| `reversi` | Classic Reversi on 8×8 with free central-square opening, win by majority disc count. Colors: **dark** and **light**. | [`games/reversi.md`](games/reversi.md) |

Each module owns: its board representation, legal-move validation, win/draw detection, and its UI rendering. **Do not extract a shared rules engine across modules** — that decision is intentional. Common features live only in the platform layer.

**Optional capabilities.** Beyond `IGameModule`, a module may opt into platform capabilities by implementing additional interfaces; the platform dispatches by capability check, never by `GameId`. Currently: `IHiddenStateGame` (Sprint 10 seam A) — per-viewer wire projection for games with hidden information; persistence keeps the full state, projection applies only while the match has no outcome, and a `null` viewer side selects the module's public view (see [`security.md`](security.md) §4 and [`state.md`](state.md) §2.3 for the delivery rules).

### 2.1 Game rules (canonical spec)

The authoritative per-game rules live in one file per game under [`docs/games/`](games/) (linked from the table above). The server validates every move against them. Per-module READMEs (`apps/api/src/PlayMe.Domain/Games/<game>/RULES.md`) may expand on edge cases, but the canonical statement lives in the game's doc.

### 2.2 Rules shared by all games

- A move that lands in an occupied cell (Tic-Tac-Toe, Reversi), a full column (Connect 4), or — for Reversi post-opening — that doesn't bracket an opponent disc, is **rejected by the server**, the player's clock keeps running, and the client must surface a clear inline error — not silently retry.
- Terminal detection runs **after every accepted move**, on the server. For the line-based games (Tic-Tac-Toe, Connect 4) the server emits `MatchEnded` with the winning line coordinates so the client can highlight them. For Reversi the server emits the final disc counts (`{ dark, light }`) instead; there is no winning line.
- Resign and timeout are platform-level outcomes (see §1 #8 and #3); they are not game-rule terminations.

## 3. Cross-game in-match UX rules

These apply to every game module's board rendering, regardless of which game it implements.

- **Last-move highlight.** Every accepted opponent move must be visually highlighted on the board for the receiving player — e.g. a subtle pulse, glow, or coloured border on the just-played cell (Tic-Tac-Toe) or the disc that just landed (Connect 4). The highlight persists until the receiving player makes their own next move, then disappears. This matters especially on the 6×6 and 9×9 boards, where scanning for a single new mark is slow. The player's *own* last move does not need this highlight; the focus is on making the opponent's action obvious.
- **Winning-line highlight.** On match end with `Outcome.Win`, the winning line is highlighted with a **distinct** visual treatment from the last-move highlight (e.g. solid glow along all winning cells vs. the pulse on a single cell). The server provides the coordinates in `MatchEnded`; the client renders them; do not recompute the winning line on the client. **Reversi has no winning line** — its renderer instead shows the final disc counts (`{ dark, light }` from `MatchEnded`) and crowns the higher-count side.
- **Series scoreboard.** When platform invariant §1 #13 applies (rematches in the same room), the in-match UI displays the current score for both players in a fixed, glanceable location (typically beside or above each player's clock).

---

## 4. Domain vocabulary

Pin canonical terms so different files don't invent synonyms. When the codebase refers to one of these concepts, use the term on the left, not a substitute.

| Term | Definition |
|---|---|
| **Room** | Container for one matchmaking session. Identified by `RoomCode`. Survives multiple matches if rematches are accepted. Has the lifecycle in [`state.md`](state.md) §2. |
| **RoomCode** | Opaque, high-entropy URL token (≥128 bits) used as the room's public identifier. Unguessable. |
| **Host** | The player who created the room. Carries the `host` role in their session token. |
| **Challenger** | The player who joined via the invite link. Carries the `challenger` role. |
| **Player** | Generic term for "host or challenger." Anonymous, identified only by a display name typed at entry. |
| **DisplayName** | A player's chosen name for this session. Sanitized, ≤24 chars. Not persistent across sessions. |
| **PlayerId** | Crypto-random 128-bit opaque ID assigned to a player on session creation (host at room creation, challenger at join). Distinct from `RoomCode`. Stored in both the session token and the room state as `hostPlayerId` / `challengerPlayerId`. Used as the authorization second factor (token's `playerId` must match the stored value on every action) and for anonymous structured logging. Not global; lives only as long as the room. Survives rematches and side swaps within the same room. |
| **Match** | One round of gameplay inside a room. A room can contain multiple matches if rematches are accepted. |
| **Move** | A player's gameplay action (Tic-Tac-Toe: `{cell: int}`, Connect 4: `{column: int}`). Validated and accepted/rejected by the server. |
| **Clock** | Per-player remaining time. Server-authoritative; ticks server-side; broadcast on move and at low frequency. |
| **TimeBank** | The fixed total time allocated to each player at match start. Same for both players. |
| **Session** / **SessionToken** | Signed credential tying a single connection to a (room, player, role) triple. HttpOnly cookie or bearer. Dies with the room. |
| **GameId** | String identifying which game module is in play: `tictactoe`, `connect4`, or `reversi`. |
| **GameOptions** | Per-room module-specific configuration carried opaquely on the room aggregate. `tictactoe` accepts `{ boardSize ∈ {3, 6, 9} }`; `connect4` and `reversi` take none. The platform never inspects the shape — the module validates via `IGameModule.ValidateOptions` (CLAUDE.md §7 "Platform thinness"). |
| **GameModule** | A self-contained per-game implementation (rules + UI). Lives in `Domain/Games/<game>/` on the API and as a feature folder on the web. |
| **Outcome** | Terminal match result: `Win` (with winner + winning-line coordinates), `Draw`, `Resign`, `Timeout`, or `Disconnect` (the server auto-ended the match after the §1 #7 reconnect grace elapsed; the disconnected side loses). |
| **RematchOffer** | The post-match handshake state. Either player can offer; opponent accepts (→ new match) or rejects (→ rejector to lobby, offerer stays with a notice). |

Don't introduce alternative terms (`Game` for `Match`, `User` for `Player`, `Token` for `Session`) without updating this table.
