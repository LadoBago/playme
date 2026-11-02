# Sprint 12 — Bot opponents (platform seam D)

Solo play: the host creates a room against a server-side bot instead of sending an invite link. The per-game author's contract lives in [`bots.md`](../bots.md); this file is the platform-side plan and the PR sequence.

## Why, and why this shape

- A player with no friend online today has nothing to do on the site. A bot is the cheapest way to make every catalog game playable alone, and it doubles as a tutorial opponent.
- The bot is a **virtual challenger inside the API**, not a browser-side player and not a separate service. A client-side bot would duplicate rules in TypeScript (forbidden: "no client-side game logic that could disagree") and, for Sea Battle, hold the opponent's fleet in the browser. A separate bot process would be a new deployable against an explicit cost model, and it would only see wire DTOs, so it would have to re-derive the rules anyway.
- This is a **deliberate platform-scope change**: who may occupy a seat. It is not a game change, and it must be framed that way in the docs (CLAUDE.md §7: "the platform changes only when its own scope changes").

## The hard rule this amends

CLAUDE.md §1 "Invite-link matchmaking only" gains one exception: a bot room has no invite link and no challenger join. Everything else — two seats, no accounts, real-time over SignalR, server authority — is unchanged. "Two players per match, exactly" still holds; one of them is a bot.

## Decisions recorded up front

- **Disconnect stays symmetric.** A host who drops mid-game in a bot room loses by disconnect after the usual grace, exactly as against a human. Zero platform changes, consistent with "the clock never pauses". Revisit only if solo players complain ([`open-questions.md`](open-questions.md)).
- **The bot has a clock.** The clock model is untouched. The bot's clock drains during its think time plus the platform's thinking delay; if the bot sweeper ever stalls, the bot loses on time. Honest and cheap.
- **Levels are a platform enum (`BotLevel { Easy, Medium, Hard }`).** It is a cross-game UI concept, so it is platform vocabulary. What a level *means* is the module's.
- **Bot-capability is derived, never declared.** `module is IBotPlayer` (plus `IBotSetup` for setup games), checked once at startup by the registry and exposed to the web through a small catalog endpoint. No game list on either side.
- **Compute outside the lock.** The room lock TTL is 5 s; a search must never run inside it. The bot turn loads the room, computes, then re-acquires the lock and re-validates side-to-move and move count before applying.
- **Hidden-state bots see the projection.** The platform projects the state for the bot side before calling `ChooseMove`, so a Sea Battle bot is honest by construction.
- **`ChallengerPicks` is rejected for bot rooms.** Side selection for a bot room is `HostPicksSpecific` or `Random`.
- **The bot auto-accepts rematches** inside `OfferRematchHandler`; the side swap works unchanged.
- **Rematch re-runs setup for the bot** via `IBotSetup`, matching "rematches re-enter setup with fresh state".
- **No shared search helper.** Three alpha-betas is the expected cost.

## Platform changes (the complete list)

| Area | Change |
|---|---|
| `Domain/Platform/` | `IBotPlayer`, `IBotSetup`, `BotLevel`, `IRandomSource` (Slice 0). `Player` gains a kind (human / bot); `Room` creation path for a pre-filled bot seat; bot seat is always connected and never grace-tracked |
| `Application/` | `CreateRoomCommand` gains opponent kind + level; `IRandom : IRandomSource`; `IBotTurnScheduler` port; `PlayBotMoveHandler` and `PlayBotSetupHandler` sharing a core with `SubmitMoveHandler` / `SubmitSetupHandler` (the shared core is extracted so the two cannot drift); `OfferRematchHandler` auto-accept; `IRoomNotifier` gains `MoveAccepted` / `MatchStarted` broadcast with per-role projection; analytics `opponentKind` property |
| `Infrastructure/` | `RedisBotTurnScheduler` + `RedisBotTurnSweeperService` (same shape as the timeout sweeper; 250 ms scan interval gives a natural floor plus a configurable thinking delay); Redis key `playme:bot_turns`; `MatchRecord`/`PlayerRecord` kind field; `GameModuleRegistry` exposes `SupportsBot(gameId)` |
| `Api/` | `GET /api/games` catalog endpoint with `supportsBot`; `CreateRoom` controller maps the new fields; `RoomNotifier` implements the new broadcasts. **No Hub change** — bot moves never enter the Hub |
| `packages/shared` | Zod + types for the catalog endpoint and the create request; i18n keys for the toggle, level names and the bot display name in both locales |
| `apps/web` | Create form: "play against a bot" toggle + level picker, gated on `supportsBot`; room page skips the invite stage for bot rooms; opponent HUD renders the bot name and icon from the kind, not from a stored string. In-match renderers: **no change** |
| Docs | CLAUDE.md §1 + docs table; `platform.md` §1 #1–#2 and §2 capabilities; `state.md` §2.1 lifecycle, §1 key schema, §2.4 invariants; `security.md` §4 (server-originated moves); `architecture.md` §3.3 (no new Hub method, one new controller) |

Bot-turn scheduling points, all platform-side: match start, every accepted move, `KeepTurn` retention, rematch accept, setup completion.

## Slices and PRs

Slice 0 lands a frozen contract with no behaviour. Slice 1 lands the whole platform and is **proven with a test-only module** so it merges with no shipped game offering "vs bot" yet. Slice 2 is one independent PR per game, in any order, by anyone.

| PR | Slice | What | Platform diff | Game diff |
|---|---|---|---|---|
| PR1 | 0 | `IBotPlayer`, `IBotSetup`, `BotLevel`, `IRandomSource` with full doc comments; [`bots.md`](../bots.md); this plan; CLAUDE.md / `platform.md` / `state.md` amendments | interfaces only | none |
| PR2 | 1 | Domain + Application: bot seat, creation, `IRandom : IRandomSource`, scheduler port, `PlayBotMoveHandler` / `PlayBotSetupHandler`, shared move core, rematch auto-accept. Tests on a `FakeBotGameModule` in `PlayMe.Application.Tests/Fakes/` (random-legal-move policy, also implements `IHiddenStateGame` + `ISetupGame` + `IBotSetup` so every seam is exercised): full match, `KeepTurn` chain, rematch, projection, stale-move discard, illegal-return forfeit | yes | none |
| PR3 | 1 | Infrastructure + Api: sweeper, scheduler, notifier broadcasts, catalog endpoint, DI, record fields; WebApplicationFactory tests against the fake module | yes | none |
| PR4 | 1 | Web + shared: toggle, level picker, skipped invite stage, bot HUD, i18n (ka + en). Toggle is disabled for every real game until a module opts in | none (web) | none |
| PR5 | 2 | `tictactoe` bot (3×3 full minimax; 6×6 / 9×9 heuristic + shallow search) | **none** | module only |
| PR6 | 2 | `connect4` bot | **none** | module only |
| PR7 | 2 | `checkers` bot | **none** | module only |
| PR8 | 2 | `reversi` bot | **none** | module only |
| PR9 | 2 | `seabattle` bot (`IBotPlayer` + `IBotSetup`) | **none** | module only |

PR5–PR9 are independent: they can land in any order, in parallel, and each one lights up its own game's toggle by adding one interface to one class. The exit check for each is the literal diff filter in [`bots.md`](../bots.md) §4.

The fake-bot test suite from PR2/PR3 is the platform's compatibility guarantee: any later platform change must keep it green, which protects every game bot without the platform knowing which exist.

## Follow-ups found while shipping the game bots

- **Mixed-version instances drop bot turns (found in PR6).** Two API builds sharing one Redis — an old one without a game's bot and a new one with it — both sweep `playme:bot_turns`. When the old instance wins the race its `PlayBotMoveHandler` reports `BotModuleFault` ("module is not an IBotPlayer") and the sweeper removes the entry, so the bot never moves and loses on time. Production runs one instance, but a rolling deploy has exactly this overlap for a few seconds whenever a game gains a bot. Fix (small platform PR): have the handler return a distinct "not capable on this instance" result and let the sweeper leave the entry for the instance that can serve it, keeping the drop only for genuine module faults. Locally: never run two API builds against the same Redis while testing bots.

## Exit criteria

- Slice 1: a bot room against the fake module plays end-to-end in tests through start, `KeepTurn`, timeout of the human, resign, rematch with side swap, setup with projection, and the stale-move and illegal-return paths; the create toggle is visible but disabled for all five games in production.
- Each game PR: the [`bots.md`](../bots.md) §4.1 checklist passes; `git diff main` outside the module folder and its tests is empty; a deployed Hard bot on the largest board stays under the 1000 ms target measured by hand.
- Existing human-vs-human play shows zero behavioural change at every PR.

## Status

**In progress.** PR1 (the contract) merged 2026-11-03 as #211. PR2 (Domain + Application half of the platform slice, proven on `FakeBotGameModule`) merged 2026-11-03 as #212. PR3 (Redis scheduler + sweeper, persistence, notifier broadcasts, `GET /api/games`) merged 2026-11-03 as #213. PR4 (web) merged 2026-11-03 as #214 — Slice 1 complete. PR5 (`tictactoe` bot) merged 2026-11-03 as #215 — first game live against the bot, end-to-end run of the seam confirmed. PR6 (`connect4` bot: centre-first alpha-beta, depth 4 / 8) in review.
