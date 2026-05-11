using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;

namespace PlayMe.Infrastructure.Games;

/// <summary>
/// Dictionary-backed <see cref="IGameModuleRegistry"/>. DI injects every
/// registered <see cref="IGameModule"/> and <see cref="IGameMoveParser"/>;
/// this class indexes them by <see cref="GameId"/> for O(1) lookup.
///
/// Lives in Infrastructure because it's pure plumbing — the Application
/// layer defines the interface and consumes it; the implementation is just
/// a composition over registered services.
/// </summary>
public sealed class GameModuleRegistry : IGameModuleRegistry
{
    private readonly Dictionary<GameId, IGameModule> _modules;
    private readonly Dictionary<GameId, IGameMoveParser> _parsers;

    public GameModuleRegistry(
        IEnumerable<IGameModule> modules,
        IEnumerable<IGameMoveParser> parsers)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(parsers);

        _modules = modules.ToDictionary(m => m.Id);
        _parsers = parsers.ToDictionary(p => p.GameId);

        // Surface configuration mistakes at startup, not on the first move:
        // every registered module must have a matching move parser.
        foreach (var id in _modules.Keys)
        {
            if (!_parsers.ContainsKey(id))
            {
                throw new InvalidOperationException(
                    $"Game '{id}' has an IGameModule but no IGameMoveParser registered.");
            }
        }
    }

    public bool IsRegistered(GameId id) => _modules.ContainsKey(id);

    public IGameModule GetModule(GameId id) =>
        _modules.TryGetValue(id, out var module)
            ? module
            : throw new KeyNotFoundException($"No IGameModule registered for '{id}'.");

    public IGameMoveParser GetMoveParser(GameId id) =>
        _parsers.TryGetValue(id, out var parser)
            ? parser
            : throw new KeyNotFoundException($"No IGameMoveParser registered for '{id}'.");
}
