using PlayMe.Application.Abstractions;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.JoinRoom;

/// <summary>
/// Registers the challenger atomically (CLAUDE.md §2.5 join contract). Runs
/// inside the room's distributed lock to serialize against any other handler
/// that might race (e.g. a second client trying to take the seat). Verifies
/// the room is joinable, resolves sides per the room's selection mode, and
/// mints a fresh <see cref="PlayerId"/> for the cookie.
/// </summary>
public sealed class JoinRoomHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IPlayerIdGenerator _playerIds;
    private readonly IGameModuleRegistry _games;
    private readonly IClock _clock;

    public JoinRoomHandler(
        IRoomRepository rooms,
        IPlayerIdGenerator playerIds,
        IGameModuleRegistry games,
        IClock clock)
    {
        _rooms = rooms;
        _playerIds = playerIds;
        _games = games;
        _clock = clock;
    }

    public async Task<AppResult<JoinRoomResult>> HandleAsync(
        JoinRoomCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        RoomCode code;
        try { code = new RoomCode(cmd.RoomCode); }
        catch (ArgumentException)
        {
            return AppResult<JoinRoomResult>.Fail(ErrorCode.RoomNotFound);
        }

        try
        {
            return await _rooms.WithLockAsync(code, async () =>
            {
                var room = await _rooms.LoadAsync(code, ct);
                if (room is null)
                {
                    return AppResult<JoinRoomResult>.Fail(ErrorCode.RoomNotFound);
                }

                if (room.Status != RoomStatus.WaitingForOpponent)
                {
                    return AppResult<JoinRoomResult>.Fail(ErrorCode.RoomNotJoinable);
                }

                if (room.Challenger is not null)
                {
                    return AppResult<JoinRoomResult>.Fail(ErrorCode.RoomAlreadyJoined);
                }

                var module = _games.GetModule(room.GameId);

                var sideCheck = ValidateChallengerSide(room.SideSelectionMode, cmd.Side, module);
                if (!sideCheck.Succeeded)
                {
                    return sideCheck.ToFailure<JoinRoomResult>();
                }

                DisplayName displayName;
                try { displayName = DisplayName.Create(cmd.DisplayName); }
                catch (ArgumentException)
                {
                    return AppResult<JoinRoomResult>.Fail(ErrorCode.ValidationDisplayName);
                }

                var challengerPlayerId = _playerIds.NewPlayerId();
                var challenger = new Player(challengerPlayerId, displayName, Side: null);

                room.RegisterChallenger(challenger, cmd.Side, module);
                await _rooms.SaveAsync(room, ct);

                return AppResult<JoinRoomResult>.Ok(
                    new JoinRoomResult(challengerPlayerId, RoomMapper.ToDto(room, _clock.UtcNow)));
            }, ct);
        }
        catch (LockTimeoutException)
        {
            return AppResult<JoinRoomResult>.Fail(ErrorCode.RoomBusy);
        }
    }

    private static AppResult<Unit> ValidateChallengerSide(
        SideSelectionMode mode, string? side, IGameModule module)
    {
        switch (mode)
        {
            case SideSelectionMode.HostPicksSpecific:
            case SideSelectionMode.Random:
                if (side is not null)
                {
                    return AppResult<Unit>.Fail(ErrorCode.JoinSideNotAllowed);
                }
                return AppResult<Unit>.Ok(Unit.Value);

            case SideSelectionMode.ChallengerPicks:
                if (side is null)
                {
                    return AppResult<Unit>.Fail(ErrorCode.JoinSidePickRequired);
                }
                if (!module.ValidSides.Contains(side))
                {
                    return AppResult<Unit>.Fail(ErrorCode.JoinInvalidSide);
                }
                return AppResult<Unit>.Ok(Unit.Value);

            default:
                return AppResult<Unit>.Fail(ErrorCode.ConfigInvalidSideSelectionMode);
        }
    }
}
