using PlayMe.Application.Abstractions;
using PlayMe.Application.Games.TicTacToe3x3;
using PlayMe.Domain.Games.TicTacToe3x3;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Tests.Fakes;

/// <summary>
/// Minimal <see cref="IGameModuleRegistry"/> wired to the only Sprint 1
/// game module. Handler tests don't need full DI assembly scanning.
/// </summary>
public sealed class SingleGameRegistry : IGameModuleRegistry
{
    private readonly TicTacToe3x3GameModule _module = new();
    private readonly TicTacToeMoveParser _parser = new();

    public bool IsRegistered(GameId id) => id == _module.Id;

    public IGameModule GetModule(GameId id)
    {
        if (id != _module.Id)
        {
            throw new InvalidOperationException($"Unknown game '{id}'.");
        }
        return _module;
    }

    public IGameMoveParser GetMoveParser(GameId id)
    {
        if (id != _module.Id)
        {
            throw new InvalidOperationException($"Unknown game '{id}'.");
        }
        return _parser;
    }
}
