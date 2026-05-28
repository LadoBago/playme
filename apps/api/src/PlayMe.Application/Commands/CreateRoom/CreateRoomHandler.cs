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

    /// <summary>
    /// Max raw JSON size of the per-room <c>gameOptions</c> blob. Surface cap
    /// applied before the game module's shape validation runs — without it a
    /// caller could attach megabytes of "padding" fields the module ignores
    /// (it only inspects known keys) and Redis would persist it. 1 KiB
    /// comfortably fits every realistic per-game options shape.
    /// </summary>
    private const int MaxGameOptionsRawJsonLength = 1024;

    private readonly IRoomRepository _rooms;
    private readonly IRoomCodeGenerator _codes;
    private readonly IPlayerIdGenerator _playerIds;
    private readonly IGameModuleRegistry _games;
    private readonly IRandom _random;
    private readonly IClock _clock;
    private readonly IRoomExpiryScheduler _expiry;

    public CreateRoomHandler(
        IRoomRepository rooms,
        IRoomCodeGenerator codes,
        IPlayerIdGenerator playerIds,
        IGameModuleRegistry games,
        IRandom random,
        IClock clock,
        IRoomExpiryScheduler expiry)
    {
        _rooms = rooms;
        _codes = codes;
        _playerIds = playerIds;
        _games = games;
        _random = random;
        _clock = clock;
        _expiry = expiry;
    }

    public async Task<AppResult<CreateRoomResult>> HandleAsync(
        CreateRoomCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (!GameId.TryCreate(cmd.GameId, out var gameId))
        {
            return AppResult<CreateRoomResult>.Fail(PlatformErrors.ConfigInvalidGameId);
        }

        if (!_games.IsRegistered(gameId))
        {
            return AppResult<CreateRoomResult>.Fail(PlatformErrors.ConfigInvalidGameId);
        }
        var module = _games.GetModule(gameId);

        // Surface size cap before the module's shape check (see constant
        // docstring for rationale). Reuses the same i18n key the module
        // returns for shape-invalid options — both are "the options blob
        // wasn't acceptable" from the caller's perspective.
        if (cmd.GameOptions is { } gameOptions
            && gameOptions.GetRawText().Length > MaxGameOptionsRawJsonLength)
        {
            return AppResult<CreateRoomResult>.Fail(PlatformErrors.ConfigInvalidGameOptions);
        }

        // Per-room options are validated by the game module (CLAUDE.md §7
        // "Platform thinness"). The platform never inspects the shape — the
        // module returns either null (accept) or an i18n error key (reject).
        var optionsError = module.ValidateOptions(cmd.GameOptions);
        if (optionsError is not null)
        {
            return AppResult<CreateRoomResult>.Fail(optionsError);
        }

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

        if (!DisplayName.TryCreate(cmd.HostDisplayName, out var displayName))
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
                gameOptions: cmd.GameOptions,
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

        // Enroll the unjoined-expiry sweeper. Deadline matches the
        // WaitingForOpponent Redis TTL so the sweeper fires within
        // moments of the room key being reaped (state.md §2.2).
        // Cancelled in RegisterPresenceHandler the moment the match
        // actually starts.
        await _expiry.ScheduleAsync(
            created.Code,
            created.GameId,
            _clock.UtcNow + RoomLifetimes.WaitingForOpponent,
            ct);

        return AppResult<CreateRoomResult>.Ok(
            new CreateRoomResult(hostPlayerId, RoomMapper.ToDto(created, _clock.UtcNow, _games)));
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
