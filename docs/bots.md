# Bot opponents — the per-game author's brief

Read this when writing a bot for one game module. It is the whole contract: you should not need to read platform code. For the platform-side plan and the PR sequence see [`roadmap/sprint-12-bots.md`](roadmap/sprint-12-bots.md).

---

## 1. What a bot is on this platform

A bot is a **virtual challenger**. The host creates a room with "play against a bot" instead of an invite link; the challenger seat is filled at creation by a bot identity, the room skips `WaitingForOpponent`, and from then on every platform feature behaves exactly as against a human: the chess clock runs for both sides, moves go through the same validate-and-commit pipeline, `KeepTurn` chains work, hidden-state projection applies, rematches swap sides, the series scoreboard counts.

The platform owns the seat, the scheduling of the bot's turn, and the commit of its move. **The module owns only the decision**: given a state and a side, which move. That decision is the one thing each game writes, independently, as a pure function.

Nothing in the client changes per game. The web renderer receives the bot's move as an ordinary `MoveAccepted` event.

## 2. The contract

Two optional capability interfaces in `Domain/Platform/`, dispatched by capability check like `IHiddenStateGame` and `ISetupGame`:

```csharp
public interface IBotPlayer
{
    GameMove ChooseMove(IGameState state, string side, BotLevel level, IRandomSource rng);
}

public interface IBotSetup   // only for modules that also implement ISetupGame
{
    GameMove ChooseSetup(JsonElement? options, string side, BotLevel level, IRandomSource rng);
}

public enum BotLevel { Easy, Medium, Hard }
```

A module becomes bot-capable by adding `IBotPlayer` (plus `IBotSetup` if it is a setup game) to its module class declaration. **That one line is the only switch.** The module registry derives a "supports bot" flag from the capability check at startup and the API exposes it to the web through the catalog endpoint; nothing is declared in a list on either side.

### 2.1 What the platform guarantees you

- `ChooseMove` is called only when the match is `InProgress`, has no outcome, and `side` is the side-to-move.
- For hidden-state modules the `state` you receive has **already been projected** for `side` (through your own `SerializeFor` and `Deserialize`). Your bot cannot see the opponent's hidden information. Do not try to work around this; it is the point.
- The call runs **outside the room lock**. The platform re-checks side-to-move and move count before applying your move, so if the human resigned or timed out while you were thinking, your move is discarded. You never need to handle that.
- The clock runs during your think time. The platform adds its own short "thinking" delay before applying your move so a 3×3 bot does not feel instant; you do not sleep.
- `rng` is the only randomness you are allowed. In production it is backed by `Random.Shared`; in tests it is seeded.
- `ChooseSetup` is called once per match (and once per rematch) before any `ChooseMove`, and the result is committed through your own `ValidateSetup` / `ApplySetup`.

### 2.2 What you guarantee the platform

- **Legality.** `ApplyMove(state, side, ChooseMove(...))` is accepted. Same for setup. An illegal return is a bug in your module; the platform logs it as an error and the bot forfeits that match. It is never retried.
- **Purity.** No I/O, no statics, no `DateTime`, no `Random.Shared`. The same `(state, side, level, seed)` produces the same move every time.
- **Boundedness.** Target: **500 ms at `Hard` on the largest board your module supports**, on the production App Service tier. Enforce it with a depth cap or a node budget, never a wall-clock timer (timers make the move depend on machine speed and break determinism). Easy and Medium must be no slower than Hard.
- **Monotonic strength.** `Hard` ≥ `Medium` ≥ `Easy` in playing strength. What each level means in your game is yours to decide and document.
- **Not an idiot at Easy.** Easy should still take an immediate win and block an immediate loss where the game has such a concept. "Random legal move" is a fine Easy for Sea Battle shooting, a poor one for Connect 4.

## 3. Where the code goes

```
apps/api/src/PlayMe.Domain/Games/<game>/
├── <Game>Module.cs      # add IBotPlayer (+ IBotSetup) to the declaration
├── <Game>Bot.cs         # the decision: search / heuristics / placement
└── RULES.md             # add a "Bot" section (see §5)

apps/api/tests/PlayMe.Application.Tests/
└── <Game>BotTests.cs    # next to the existing <Game>GameModuleTests.cs
```

Keep the search inside `<Game>Bot.cs` (or a small folder under the module if it grows). The module class delegates to it so the rules file stays about rules.

**No shared search helper.** Connect 4, Reversi and Checkers will each write their own alpha-beta. That duplication is expected and accepted (CLAUDE.md §7 "Platform thinness"). If three modules end up with the byte-identical core, extract a utility that modules *choose to instantiate* — never something the platform knows about or routes through.

## 4. The per-game PR

One PR per game. It touches the module folder, its tests, and its `RULES.md` — **nothing else**. The exit check is literal:

```bash
git diff main --stat -- . ':!apps/api/src/PlayMe.Domain/Games/<game>' ':!apps/api/tests/PlayMe.Application.Tests/<Game>BotTests.cs'
```

must be empty. If your bot needs a platform edit, a web edit, or an i18n key, stop: the seam is wrong. Fix it in a separate platform PR first, then come back.

### 4.1 Test checklist

Every game bot PR ships tests for:

- **Legality everywhere.** For a sample of reachable states (random playouts under a seeded rng, a few hundred positions), every level returns a move `ApplyMove` accepts. For setup games, `ChooseSetup` validates.
- **Determinism.** Same inputs and seed, same move, across two calls.
- **Boundedness.** Hard on the largest board stays under the budget in a test that measures elapsed time with a generous ceiling (say 2 s on a laptop; the 500 ms target is for the production tier and is checked by hand on a deployed build).
- **Tactics.** Where the game has them: takes an available immediate win; blocks an opponent's immediate win; in Checkers, respects mandatory capture (the rules enforce it, so this is a legality test in disguise). In Sea Battle: after a hit, the next shot is adjacent; after a sink, hunting resumes.
- **Strength ordering.** Hard beats Easy in a seeded head-to-head over N games with a clear majority. Keep N small enough that the suite stays fast.

## 5. Documenting your levels

Add a short **Bot** section to the module's `RULES.md` (or the game doc under `docs/games/`): one line per level saying what it does, e.g. "Easy: depth 2, random tie-break. Medium: depth 4. Hard: depth 7 with centre-column ordering." That is the only user-facing-adjacent text, and it is for maintainers; the web shows the level name only, from a platform i18n key.

## 6. Suggested approaches

Not prescriptions. Pick what gives a credible Hard inside the budget.

| Game | Easy | Hard | Notes |
|---|---|---|---|
| Tic-Tac-Toe 3×3 | win/block, else random | full minimax | solved game; Hard never loses |
| Tic-Tac-Toe 6×6 / 9×9 | win/block, else near a stone | threat-space heuristic + shallow search | same module; key depth by `boardSize` |
| Connect 4 | win/block, else random | alpha-beta ~depth 8, centre-first ordering | bitboards are optional |
| Checkers | random legal move from the published list | alpha-beta over the legal-move list, material + kings + advancement | legal moves already exist on the state |
| Reversi | random legal move | alpha-beta with positional weights + mobility, corners first | tune by self-play before calling it Hard |
| Sea Battle | random untried cell | hunt / target with parity, random valid fleet | `IBotSetup` places the fleet; no search |

## 7. Things that are not your problem

- Whether the bot's turn fires, when, or on which API instance.
- Clock, timeouts, disconnects, rematch acceptance, scoreboard.
- Broadcasting the move or projecting it for the human.
- The create-form toggle, the bot's display name, the level picker, i18n.
- Rate limits and session auth. Bot moves originate server-side and never pass through the Hub.
