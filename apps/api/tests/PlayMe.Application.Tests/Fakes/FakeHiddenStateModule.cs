using System.Text.Json;
using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Tests.Fakes;

/// <summary>
/// Minimal hidden-state game module for seam-A tests. The "state" is an
/// opaque token; <see cref="SerializeFor"/> tags it with the viewer so
/// tests can assert which projection each viewer received without a real
/// game's rules in the way.
/// </summary>
public sealed class FakeHiddenStateModule : IGameModule, IHiddenStateGame
{
    public static readonly GameId Id_ = new("fakehidden");

    private sealed record FakeState(string Token) : IGameState;

    public GameId Id => Id_;

    public IReadOnlyList<string> ValidSides { get; } = new[] { "first", "second" };

    public string FirstMoveSide => "first";

    public TimeSpan DefaultClockBudget => TimeSpan.FromMinutes(1);

    public string? ValidateOptions(JsonElement? options) => null;

    public IGameState NewMatch(JsonElement? options) => new FakeState("full");

    public MoveResult ApplyMove(IGameState state, string side, GameMove move) =>
        MoveResult.Accept(state);

    public string OtherSide(string side) => side == "first" ? "second" : "first";

    public string Serialize(IGameState state) => ((FakeState)state).Token;

    public IGameState Deserialize(string serialized) => new FakeState(serialized);

    public string SerializeFor(IGameState state, string? viewerSide) =>
        viewerSide is null
            ? $"{((FakeState)state).Token}:public"
            : $"{((FakeState)state).Token}:view-{viewerSide}";
}

/// <summary>
/// Single-module <see cref="IGameModuleRegistry"/> over an arbitrary
/// module instance — <see cref="SingleGameRegistry"/> is hard-wired to
/// Tic-Tac-Toe, which can't exercise the hidden-state capability path.
/// </summary>
public sealed class StubModuleRegistry : IGameModuleRegistry
{
    private readonly IGameModule _module;

    public StubModuleRegistry(IGameModule module) => _module = module;

    public bool IsRegistered(GameId id) => id == _module.Id;

    public IGameModule GetModule(GameId id)
    {
        if (id != _module.Id)
        {
            throw new InvalidOperationException($"Unknown game '{id.Value}'.");
        }
        return _module;
    }

    public IGameMoveParser GetMoveParser(GameId id) =>
        throw new NotSupportedException("Seam-A tests never parse moves.");
}
