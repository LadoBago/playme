using PlayMe.Domain.Platform;

namespace PlayMe.Application.Abstractions;

/// <summary>
/// Dispatches by <see cref="GameId"/> to the <see cref="IGameModule"/> (rules)
/// and <see cref="IGameMoveParser"/> (DTO → <see cref="GameMove"/>) for that
/// game. Implemented in Infrastructure by composing DI-registered modules
/// and parsers into dictionaries.
///
/// The split between module (Domain, pure rules) and parser (Application,
/// wire-shape) keeps the Domain free of Application DTO types per CLAUDE.md
/// §2.4 dependency rule.
/// </summary>
public interface IGameModuleRegistry
{
    /// <summary>True if the game id has a registered module + parser.</summary>
    bool IsRegistered(GameId id);

    /// <summary>Look up the game module. Throws if the id isn't registered.</summary>
    IGameModule GetModule(GameId id);

    /// <summary>Look up the move parser. Throws if the id isn't registered.</summary>
    IGameMoveParser GetMoveParser(GameId id);
}
