using PlayMe.Application.Abstractions;
using PlayMe.Application.Commands.CreateRoom;
using PlayMe.Application.Dtos;

namespace PlayMe.Application.Queries.GetGameCatalog;

/// <summary>
/// Lists every registered game with its server-derived capabilities
/// (Sprint 12 seam D). Pure composition over the registry: adding a bot
/// to a game changes this output by the module gaining an interface, not
/// by anyone editing a list (docs/bots.md §2).
/// </summary>
public sealed class GetGameCatalogHandler
{
    private readonly IGameModuleRegistry _games;

    public GetGameCatalogHandler(IGameModuleRegistry games)
    {
        _games = games;
    }

    public IReadOnlyList<GameCatalogEntryDto> Handle() =>
        _games.GameIds
            .Select(id => new GameCatalogEntryDto(id, CreateRoomHandler.SupportsBot(_games.GetModule(id))))
            .ToList();
}
