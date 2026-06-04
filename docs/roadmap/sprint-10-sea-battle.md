# Sprint 10 — Sea Battle (~2 weeks)

First **hidden-information** game. Canonical rules in [`games/seabattle.md`](../games/seabattle.md) (ka „ჩაძირობანა"; post-Soviet ruleset: 10-ship fleet, no-touch placement, hit = extra shot).

Unlike every game so far, this one **cannot land with zero platform edits** — the current seams have three hard gaps:

1. `IGameModule.Serialize` produces one blob used for both Redis and the wire (`MatchDto.State`), and state-bearing events broadcast to the whole SignalR room group — both players see identical state. Sea battle needs each player to see their own fleet but not the opponent's.
2. `SubmitMoveHandler` always flips the turn and `MoveResult` carries no override — the hit-shoots-again rule can't be expressed.
3. There is no pre-match, simultaneous, secret setup step — rooms go `WaitingForOpponent → InProgress` and the clock starts immediately.

The fix is **three deliberate platform scope extensions** (CLAUDE.md §7: *"the platform changes only when its own scope changes — explicit, deliberate, discussed changes"*; discussed and agreed 2026-06-04). Each seam lands additively and independently **before** any game code, mirroring the Sprint 9 PR1a precedent: opt-in, opaque, invisible to existing games. The next hidden-information game, the next extra-turn game, and the next setup game then ride them with zero platform edits.

## Seam A — per-viewer state projection (hidden information)

- New **optional** interface in `Domain/Platform/` (`IGameModule` itself doesn't change; existing modules untouched):

  ```csharp
  public interface IHiddenStateGame
  {
      /// Wire-facing projection of state for one viewer. Persistence
      /// always uses IGameModule.Serialize (full state).
      string SerializeFor(IGameState state, string viewerSide);
  }
  ```

- **Delivery:** for modules implementing it, state-bearing payloads (`MatchStarted`, `MoveAccepted`, `SetupStarted`, presence/snapshot responses, REST `GET /api/rooms/{code}`, `JoinInfo`) are projected per the receiving role. SignalR side: register each connection into a per-role group (`room:{code}:host` / `room:{code}:challenger`) alongside the existing room group at presence registration; state-bearing events for hidden-state games send two projected DTOs to the two role groups instead of one to the room group. Role groups ride the existing Redis backplane and survive reconnects. Non-state events (`RematchOffered`, `OpponentDisconnected`, …) keep using the room group.
- **Terminal reveal rule (platform policy):** once the match has an `Outcome` — any outcome, including resign/timeout/disconnect — the platform ships the full unprojected `Serialize` state to both players. Hiding is moot after the match; the loser gets to see the fleet. Projection is consulted only while `Outcome == null`.
- **Persistence unchanged:** Redis always stores the full `Serialize` blob; projection happens at the wire boundary only. The opponent's fleet never crosses the wire pre-terminal — server-authority taken to its conclusion ([`security.md`](../security.md) gets a note in the seam PR).
- Dispatch is always `module is IHiddenStateGame` — never `if (gameId == "seabattle")`.

## Seam B — turn retention (`MoveResult.KeepTurn`)

- `MoveResult.Accept(IGameState newState, Outcome? ending = null, bool keepTurn = false)` — new optional flag; the default preserves current behavior for all existing modules. `SubmitMoveHandler` computes `nextSide = keepTurn ? callerSide : OtherSide(callerSide)`; the no-move timeout reschedules for whichever side is next, unchanged.
- **Why not the Sprint 8 synthetic-pass pattern?** The [`sprint-08-reversi.md`](sprint-08-reversi.md) design note claimed that seam would cover "Mancala same-side-again" — it doesn't survive contact with sea battle, where extra turns occur on *every hit* (a large fraction of all moves, not a rare edge case): (a) the opponent's clock would tick during their forced no-op "turn" — charging the defender time for the shooter's success is a clock-fairness violation; (b) every hit would cost an extra client→server round-trip of dead air; (c) a disconnected defender would have the abandon-grace timer started against them on a "turn" in which they have no decision to make. **Rule of thumb going forward:** renderer-emitted synthetic moves for *rare, decision-free* skips (Reversi pass); `KeepTurn` for *frequent, earned* extra turns. The Sprint 8 note gets a scope-limit amendment in the seam PR.
- Platform invariant amendment ([`platform.md`](../platform.md) §1, in the seam PR): the platform no longer guarantees strict alternation; it guarantees *the module decides turn retention per accepted move, and the platform enforces whose turn it is*.

## Seam C — module-declared setup phase

- New **optional** interface:

  ```csharp
  public interface ISetupGame
  {
      /// Per-game setup window, measured from SettingUp entry.
      TimeSpan SetupBudget { get; }
      /// Validate one side's setup payload. Null on success or a
      /// module-owned reject key.
      string? ValidateSetup(IGameState state, string side, GameMove setup);
      /// Apply a validated setup. The module records which sides have
      /// committed; the platform asks via IsSetupComplete.
      IGameState ApplySetup(IGameState state, string side, GameMove setup);
      bool IsSetupComplete(IGameState state);
  }
  ```

- New room status **`SettingUp`** between `WaitingForOpponent` and `InProgress`: when both players are registered + connected and the module implements `ISetupGame`, the room enters `SettingUp` instead of `InProgress` (`NewMatch` runs at `SettingUp` entry to create the empty pre-battle state). `IsSetupComplete` flips it to `InProgress` → `MatchStarted`, clock starts. Setup-less games skip straight to `InProgress` exactly as today. `IsSetupComplete` is answered by the module, not by the platform counting commits — a future game can have asymmetric setup without the platform caring.
- New Hub method **`SubmitSetup`** (same auth as `SubmitMove`: session token + room role + room lock; valid only in `SettingUp`; payload parsed by the module's `IGameMoveParser`, opaque to the platform).
- Events: entering `SettingUp` emits **`SetupStarted`** (role-projected state via seam A); a commit emits **`OpponentSetupCommitted`** to the other player (role only, no payload).
- **Clock-start amendment** ([`platform.md`](../platform.md) §1 #12, in the seam PR): *the clock starts when the match enters `InProgress`* — immediately on both-present for setup-less games, immediately on both-committed for setup games. Setup itself is unclocked.
- **Setup deadline:** one entry scheduled at `SettingUp` entry + `SetupBudget` (2 min for sea battle), following the existing sorted-set sweeper pattern. On fire: exactly one side uncommitted → that side forfeits with `Outcome.Timeout(uncommittedSide)` (rolls into the opponent's scoreboard win, like every clock-family outcome); both uncommitted → room goes terminal via the `RoomExpired` path (no match outcome; in a rematch series this discards the scoreboard — accepted for v1, both players walked away mid-handshake).
- **Presence during setup is tracked like `InProgress`, not like `WaitingForOpponent`** (user decision): a disconnect by an *uncommitted* player schedules a disconnect-grace entry per the existing tier table ([`platform.md`](../platform.md) §1 #7, keyed on the configured clock budget); grace elapse → `Outcome.Disconnect(disconnectedSide)`, opponent wins. The §1 #7 "only ticks on the disconnected player's turn" condition doesn't apply — setup has no turns; the grace starts immediately. A disconnect by a player who has *already committed* schedules nothing during setup — they owe nothing until the match starts; if setup completes while they're offline, the room enters `InProgress` and the standard in-match clock + grace rules take over. Reconnect cancels the pending entry, as in-match.
- Rematch flow: `AwaitingRematch` --accept--> `SettingUp` (for setup games) → `InProgress`, fresh fleets every match. Side swap applies at the same point as today.
- [`state.md`](../state.md) doc touches (in the seam PR): state-machine diagram + §2.1 states, §1 key schema (setup-deadline entries), §2.3 events table, TTL while `SettingUp`.

## Fleet placement (v1 product decision)

Random + reroll, client-generated, server-validated — full contract in [`games/seabattle.md`](../games/seabattle.md). Key properties: reroll is client-local (instant, zero server calls, `crypto.getRandomValues`); commit is one `SubmitSetup` call validated exhaustively by the module; the opponent sees readiness only. **Manual placement is deferred** — same commit endpoint, only the setup UI changes; added to [`deferred-polish.md`](deferred-polish.md).

## The game module + web (pure addition, after the seams)

- `Domain/Games/SeaBattle/` — state (two fleets, two shot maps, commit flags), `IGameMoveParser` for the `{ x, y }` shot and `{ ships: […] }` setup payloads, module implementing `IGameModule` + `IHiddenStateGame` + `ISetupGame`. DI registration (one line each in `AddDomain.cs` / `AddApplication.cs`), catalog entry.
- Web renderer at `apps/web/features/games/seabattle/` per the renderer contract in [`games/seabattle.md`](../games/seabattle.md); catalog `registry.ts` entry — landing grid grows to **four** tiles.
- i18n: `games.seabattle.*` in **both** `ka.ts` and `en.ts`. ka name **„ჩაძირობანა"** (decided with the native speaker).
- `/play/seabattle` ships the full SEO surface (metadata, canonical, hreflang, OG/Twitter, JSON-LD `Game` schema, sitemap entry) — a page without metadata is incomplete.

## PR breakdown (each squash-merged, in order)

| PR | What | Platform diff allowed? |
|---|---|---|
| PR1 | docs: this plan + [`games/seabattle.md`](../games/seabattle.md) | — |
| PR2 | Seam A — `IHiddenStateGame`, per-role groups, projected delivery, terminal-reveal rule + tests + `security.md` note | yes — seam A only |
| PR3 | Seam B — `MoveResult.KeepTurn` + handler + timeout rescheduling + tests + `platform.md` alternation amendment + Sprint 8 note scope-limit | yes — seam B only |
| PR4 | Seam C — `SettingUp` status, `SubmitSetup`, setup-deadline sweeper, setup presence rules, events + tests + `platform.md`/`state.md` touches | yes — seam C only |
| PR5 | `seabattle` domain module + parser + module tests (no catalog entry yet — unreachable) | **none** |
| PR6 | web renderer + catalog entry + i18n + SEO + sitemap (cutover to 4 tiles) + `CLAUDE.md` §1 catalog line + [`open-questions.md`](open-questions.md) "More games" count | **none** |

Cross-cutting doc touches land inside the listed PRs, not after ([`sprint-09-game-options.md`](sprint-09-game-options.md) precedent).

## Exit criteria

- Sea battle plays end-to-end with clock, reconnect, resign, rematch (side swap alternates the first shot), and the series scoreboard.
- **Hidden information verified adversarially:** the browser network tab never contains opponent fleet data before `MatchEnded` — across moves, reconnect snapshots, REST fetches, and join info.
- Hit-chains retain the turn; the defender's clock never ticks during them.
- Setup: reroll/commit verified; both-committed starts the clock; the 2-min deadline forfeits an uncommitted side; a mid-setup disconnect adjudicates per the grace rules; rematch re-enters setup with fresh fleets.
- Existing three games show **zero behavioral change**; each seam PR's platform diff is confined to its seam (`git diff` check per the Sprint 8/9 precedent).
- Lighthouse green on `/play/seabattle` in both locales and both themes.
