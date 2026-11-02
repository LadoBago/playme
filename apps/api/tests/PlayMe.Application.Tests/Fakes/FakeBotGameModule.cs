using System.Text.Json;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Dtos;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Tests.Fakes;

/// <summary>
/// The platform's bot compatibility module (Sprint 12 seam D;
/// docs/roadmap/sprint-12-bots.md PR2). A deliberately tiny "race" game
/// that still exercises every seam a real bot can touch:
/// <list type="bullet">
///   <item>Each side has a counter; a <see cref="Step"/> of 1 or 2 advances
///   the mover's counter. Reaching <see cref="Target"/> wins.</item>
///   <item>A step of 2 retains the turn (<see cref="MoveResult.KeepTurn"/>).</item>
///   <item>Hidden state: a viewer sees only their own counter; the other
///   side's reads <see cref="Masked"/>. The bot must play from this view.</item>
/// </list>
/// The bot policy is a plain <c>Step(1)</c> unless a test overrides it;
/// <see cref="OnThinking"/> runs inside <see cref="ChooseMove"/> so a test
/// can mutate the room "while the bot is thinking" and prove the handler
/// discards the stale decision. The setup-phase variant is
/// <see cref="FakeBotSetupGameModule"/>.
/// </summary>
public class FakeBotGameModule : IGameModule, IBotPlayer, IHiddenStateGame
{
    public static readonly GameId Id_ = new("fakebot");
    public const int Target = 5;
    public const int Masked = -1;
    public const string First = "first";
    public const string Second = "second";

    internal sealed record RaceState(int First, int Second, bool FirstSetup, bool SecondSetup) : IGameState;

    internal sealed record Step(int N) : GameMove;

    internal sealed record Start(int At) : GameMove;

    /// <summary>Replaces the default <c>Step(1)</c> policy for the next calls.</summary>
    public Func<IGameState, string, BotLevel, IRandomSource, GameMove>? ChooseMoveOverride { get; set; }

    /// <summary>Invoked inside <see cref="ChooseMove"/> before returning — the
    /// test's hook for "something happened while the bot was thinking".</summary>
    public Func<Task>? OnThinking { get; set; }

    /// <summary>Every state handed to <see cref="ChooseMove"/>, for projection assertions.</summary>
    public List<IGameState> SeenStates { get; } = new();

    public List<BotLevel> SeenLevels { get; } = new();

    public virtual GameId Id => Id_;

    public IReadOnlyList<string> ValidSides { get; } = new[] { First, Second };

    public string FirstMoveSide => First;

    public TimeSpan ClockBudgetFor(JsonElement? options) => TimeSpan.FromMinutes(1);

    public string? ValidateOptions(JsonElement? options) => null;

    public IGameState NewMatch(JsonElement? options) => new RaceState(0, 0, false, false);

    public MoveResult ApplyMove(IGameState state, string side, GameMove move)
    {
        var race = (RaceState)state;
        if (move is not Step { N: 1 or 2 } step)
        {
            return MoveResult.Reject("fakebot.illegalStep");
        }

        var next = side == First
            ? race with { First = race.First + step.N }
            : race with { Second = race.Second + step.N };
        var own = side == First ? next.First : next.Second;
        return MoveResult.Accept(
            next,
            ending: own >= Target ? new Win(side) : null,
            keepTurn: step.N == 2);
    }

    public string OtherSide(string side) => side == First ? Second : First;

    public string Serialize(IGameState state) => JsonSerializer.Serialize((RaceState)state);

    public IGameState Deserialize(string serialized) =>
        JsonSerializer.Deserialize<RaceState>(serialized)
        ?? throw new ArgumentException("Not a fakebot state.", nameof(serialized));

    public string SerializeFor(IGameState state, string? viewerSide)
    {
        var race = (RaceState)state;
        var projected = viewerSide switch
        {
            First => race with { Second = Masked },
            Second => race with { First = Masked },
            _ => race with { First = Masked, Second = Masked },
        };
        return JsonSerializer.Serialize(projected);
    }

    public GameMove ChooseMove(IGameState state, string side, BotLevel level, IRandomSource rng)
    {
        SeenStates.Add(state);
        SeenLevels.Add(level);
        OnThinking?.Invoke().GetAwaiter().GetResult();
        return ChooseMoveOverride is { } policy
            ? policy(state, side, level, rng)
            : new Step(1);
    }

    /// <summary>Own counter from the module's state, for assertions.</summary>
    public static int CounterOf(IGameState state, string side) =>
        side == First ? ((RaceState)state).First : ((RaceState)state).Second;
}

/// <summary>
/// <see cref="FakeBotGameModule"/> with a setup phase: each side commits a
/// <see cref="FakeBotGameModule.Start"/> value in 0..2 for its counter.
/// The bot's default setup is <c>Start(0)</c>; <see cref="ChooseSetupOverride"/>
/// lets a test make it illegal.
/// </summary>
public sealed class FakeBotSetupGameModule : FakeBotGameModule, ISetupGame, IBotSetup
{
    public static readonly GameId SetupId = new("fakebotsetup");

    public override GameId Id => SetupId;

    public Func<JsonElement?, string, BotLevel, IRandomSource, GameMove>? ChooseSetupOverride { get; set; }

    public TimeSpan SetupBudget => TimeSpan.FromMinutes(2);

    public string? ValidateSetup(IGameState state, string side, GameMove setup) =>
        setup is Start { At: >= 0 and <= 2 } ? null : "fakebot.badStart";

    public IGameState ApplySetup(IGameState state, string side, GameMove setup)
    {
        var race = (RaceState)state;
        var at = ((Start)setup).At;
        return side == First
            ? race with { First = at, FirstSetup = true }
            : race with { Second = at, SecondSetup = true };
    }

    public bool IsSetupComplete(IGameState state) =>
        ((RaceState)state) is { FirstSetup: true, SecondSetup: true };

    public GameMove ChooseSetup(JsonElement? options, string side, BotLevel level, IRandomSource rng) =>
        ChooseSetupOverride is { } policy
            ? policy(options, side, level, rng)
            : new Start(0);
}

/// <summary>Human-side parser: <c>{ n }</c> is a step, <c>{ at }</c> a setup.</summary>
public sealed class FakeBotMoveParser : IGameMoveParser
{
    public FakeBotMoveParser(GameId gameId)
    {
        GameId = gameId;
    }

    public GameId GameId { get; }

    public AppResult<GameMove> Parse(MoveDto dto)
    {
        if (dto.Payload.TryGetProperty("n", out var n))
        {
            return AppResult<GameMove>.Ok(new FakeBotGameModule.Step(n.GetInt32()));
        }
        if (dto.Payload.TryGetProperty("at", out var at))
        {
            return AppResult<GameMove>.Ok(new FakeBotGameModule.Start(at.GetInt32()));
        }
        return AppResult<GameMove>.Fail("fakebot.badPayload");
    }
}
