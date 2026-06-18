using System.Text.Json;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Dtos;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Tests.Fakes;

/// <summary>
/// Minimal setup-phase game module for seam-C tests. Setup state is just
/// the set of sides that have applied a setup; <see cref="IsSetupComplete"/>
/// requires both. <see cref="NextRejectKey"/> lets a test drive a module-
/// owned validation failure for the next <see cref="ValidateSetup"/> call.
/// </summary>
public sealed class FakeSetupGameModule : IGameModule, ISetupGame
{
    public static readonly GameId Id_ = new("fakesetup");

    internal sealed record SetupState(IReadOnlyList<string> CommittedSides) : IGameState;

    internal sealed record FakeSetupMove : GameMove;

    /// <summary>Set to make the next ValidateSetup call reject.</summary>
    public string? NextRejectKey { get; set; }

    public GameId Id => Id_;

    public IReadOnlyList<string> ValidSides { get; } = new[] { "first", "second" };

    public string FirstMoveSide => "first";

    public TimeSpan ClockBudgetFor(JsonElement? options) => TimeSpan.FromMinutes(3);

    public TimeSpan SetupBudget => TimeSpan.FromMinutes(2);

    public string? ValidateOptions(JsonElement? options) => null;

    public IGameState NewMatch(JsonElement? options) =>
        new SetupState(Array.Empty<string>());

    public MoveResult ApplyMove(IGameState state, string side, GameMove move) =>
        MoveResult.Accept(state);

    public string OtherSide(string side) => side == "first" ? "second" : "first";

    public string Serialize(IGameState state) =>
        string.Join(",", ((SetupState)state).CommittedSides);

    public IGameState Deserialize(string serialized) =>
        new SetupState(serialized.Length == 0
            ? Array.Empty<string>()
            : serialized.Split(','));

    public string? ValidateSetup(IGameState state, string side, GameMove setup)
    {
        var key = NextRejectKey;
        NextRejectKey = null;
        return key;
    }

    public IGameState ApplySetup(IGameState state, string side, GameMove setup)
    {
        var committed = ((SetupState)state).CommittedSides.Append(side).ToArray();
        return new SetupState(committed);
    }

    public bool IsSetupComplete(IGameState state) =>
        ((SetupState)state).CommittedSides.Count == 2;
}

/// <summary>Parser counterpart — the payload carries nothing the platform tests need.</summary>
public sealed class FakeSetupMoveParser : IGameMoveParser
{
    public GameId GameId => FakeSetupGameModule.Id_;

    public AppResult<GameMove> Parse(MoveDto dto) =>
        AppResult<GameMove>.Ok(new FakeSetupGameModule.FakeSetupMove());
}
