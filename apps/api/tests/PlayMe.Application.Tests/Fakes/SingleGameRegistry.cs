using PlayMe.Application.Abstractions;
using PlayMe.Application.Games.TicTacToe;
using PlayMe.Domain.Games.TicTacToe;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Tests.Fakes;

/// <summary>
/// Minimal <see cref="IGameModuleRegistry"/> wired to a single game
/// module — the unified Tic-Tac-Toe (Sprint 9 PR3). Handler tests don't
/// need full DI assembly scanning.
/// </summary>
public sealed class SingleGameRegistry : IGameModuleRegistry
{
    private readonly TicTacToeGameModule _module = new();
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
