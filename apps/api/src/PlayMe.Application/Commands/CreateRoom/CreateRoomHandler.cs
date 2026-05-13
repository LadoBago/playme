using PlayMe.Application.Abstractions;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.CreateRoom;

/// <summary>
/// Allocates a new room (CLAUDE.md §2.5 configure flow). Validates the game
/// module exists; resolves the host's side per <see cref="SideSelectionMode"/>;
/// generates an opaque room code + host player id with the cryptographic
/// RNGs per §5.4; persists the room via <see cref="IRoomRepository.CreateAsync"/>
/// (retries on the astronomically unlikely code collision).
/// </summary>
public sealed class CreateRoomHandler
{
    private const int CodeCollisionRetryLimit = 5;

    private readonly IRoomRepository _rooms;
    private readonly IRoomCodeGenerator _codes;
    private readonly IPlayerIdGenerator _playerIds;
    private readonly IGameModuleRegistry _games;
    private readonly IRandom _random;
    private readonly IClock _clock;

    public CreateRoomHandler(
        IRoomRepository rooms,
        IRoomCodeGenerator codes,
        IPlayerIdGenerator playerIds,
        IGameModuleRegistry games,
        IRandom random,
        IClock clock)
    {
        _rooms = rooms;
        _codes = codes;
        _playerIds = playerIds;
        _games = games;
        _random = random;
        _clock = clock;
    }

    public async Task<AppResult<CreateRoomResult>> HandleAsync(
        CreateRoomCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        GameId gameId;
        try { gameId = new GameId(cmd.GameId); }
        catch (ArgumentException)
        {
            return AppResult<CreateRoomResult>.Fail(PlatformErrors.ConfigInvalidGameId);
        }

        if (!_games.IsRegistered(gameId))
        {
            return AppResult<CreateRoomResult>.Fail(PlatformErrors.ConfigInvalidGameId);
        }
        var module = _games.GetModule(gameId);

        if (!Enum.IsDefined(cmd.SideSelectionMode))
        {
            return AppResult<CreateRoomResult>.Fail(PlatformErrors.ConfigInvalidSideSelectionMode);
        }

        var sideResult = ResolveHostSide(cmd.SideSelectionMode, cmd.HostSide, module);
        if (!sideResult.Succeeded)
        {
            return sideResult.ToFailure<CreateRoomResult>();
        }
        var hostSide = sideResult.Value;

        DisplayName displayName;
        try { displayName = DisplayName.Create(cmd.HostDisplayName); }
        catch (ArgumentException)
        {
            return AppResult<CreateRoomResult>.Fail(PlatformErrors.ValidationDisplayName);
        }

        var hostPlayerId = _playerIds.NewPlayerId();
        var host = new Player(hostPlayerId, displayName, hostSide);

        Room? created = null;
        for (var attempt = 0; attempt < CodeCollisionRetryLimit; attempt++)
        {
            var candidate = Room.Create(
                code: _codes.NewCode(),
                gameId: gameId,
                sideSelectionMode: cmd.SideSelectionMode,
                host: host,
                createdAt: _clock.UtcNow);

            if (await _rooms.CreateAsync(candidate, ct))
            {
                created = candidate;
                break;
            }
        }

        if (created is null)
        {
            return AppResult<CreateRoomResult>.Fail(
                PlatformErrors.RoomBusy,
                "Repeated room-code collisions; aborting room creation.");
        }

        return AppResult<CreateRoomResult>.Ok(
            new CreateRoomResult(hostPlayerId, RoomMapper.ToDto(created, _clock.UtcNow)));
    }

    private AppResult<string?> ResolveHostSide(
        SideSelectionMode mode, string? requestedHostSide, IGameModule module)
    {
        switch (mode)
        {
            case SideSelectionMode.HostPicksSpecific:
                if (requestedHostSide is null || !module.ValidSides.Contains(requestedHostSide))
                {
                    return AppResult<string?>.Fail(PlatformErrors.ConfigInvalidHostSide);
                }
                return AppResult<string?>.Ok(requestedHostSide);

            case SideSelectionMode.Random:
                if (requestedHostSide is not null)
                {
                    return AppResult<string?>.Fail(
                        PlatformErrors.ConfigInvalidHostSide,
                        "Host side must not be provided under Random mode.");
                }
                var pick = module.ValidSides[_random.NextInt(module.ValidSides.Count)];
                return AppResult<string?>.Ok(pick);

            case SideSelectionMode.ChallengerPicks:
                if (requestedHostSide is not null)
                {
                    return AppResult<string?>.Fail(
                        PlatformErrors.ConfigInvalidHostSide,
                        "Host side must not be provided under ChallengerPicks mode.");
                }
                return AppResult<string?>.Ok(null);

            default:
                return AppResult<string?>.Fail(PlatformErrors.ConfigInvalidSideSelectionMode);
        }
    }
}
