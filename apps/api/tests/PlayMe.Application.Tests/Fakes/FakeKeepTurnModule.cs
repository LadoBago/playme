using System.Text.Json;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Dtos;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Tests.Fakes;

/// <summary>
/// Minimal extra-turn game module for seam-B tests. Every accepted move
/// echoes the wire payload's <c>keepTurn</c> / <c>win</c> flags back
/// through <see cref="MoveResult"/>, so a test drives turn retention and
/// match endings directly from the submitted move without real rules.
/// </summary>
public sealed class FakeKeepTurnModule : IGameModule
{
    public static readonly GameId Id_ = new("fakekeepturn");

    private sealed record FakeState : IGameState;

    internal sealed record FakeMove(bool KeepTurn, bool Win) : GameMove;

    public GameId Id => Id_;

    public IReadOnlyList<string> ValidSides { get; } = new[] { "first", "second" };

    public string FirstMoveSide => "first";

    public TimeSpan DefaultClockBudget => TimeSpan.FromMinutes(1);

    public string? ValidateOptions(JsonElement? options) => null;

    public IGameState NewMatch(JsonElement? options) => new FakeState();

    public MoveResult ApplyMove(IGameState state, string side, GameMove move)
    {
        var fake = (FakeMove)move;
        return MoveResult.Accept(
            state,
            ending: fake.Win ? new Win(side) : null,
            keepTurn: fake.KeepTurn);
    }

    public string OtherSide(string side) => side == "first" ? "second" : "first";

    public string Serialize(IGameState state) => "{}";

    public IGameState Deserialize(string serialized) => new FakeState();
}

/// <summary>Parser counterpart — reads <c>{ keepTurn, win }</c> off the wire payload.</summary>
public sealed class FakeKeepTurnMoveParser : IGameMoveParser
{
    public GameId GameId => FakeKeepTurnModule.Id_;

    public AppResult<GameMove> Parse(MoveDto dto)
    {
        var keepTurn = dto.Payload.TryGetProperty("keepTurn", out var k) && k.GetBoolean();
        var win = dto.Payload.TryGetProperty("win", out var w) && w.GetBoolean();
        return AppResult<GameMove>.Ok(new FakeKeepTurnModule.FakeMove(keepTurn, win));
    }
}
