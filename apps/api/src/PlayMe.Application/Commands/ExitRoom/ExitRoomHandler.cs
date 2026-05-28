using PlayMe.Application.Abstractions;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Application.RateLimiting;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.ExitRoom;

/// <summary>
/// Exit-from-room pipeline (docs/state.md §2.4). Caller voluntarily ends
/// the room session post-match — the room transitions to
/// <see cref="RoomStatus.Closed"/> and the still-present player gets an
/// <c>OpponentExited</c> broadcast. The same handler is reused from the
/// hub's tab-close path so an unloaded tab in <see cref="RoomStatus.Ended"/>
/// or <see cref="RoomStatus.AwaitingRematch"/> closes the room identically
/// to an explicit "Back to lobby" click.
///
/// Idempotent on <see cref="RoomStatus.Closed"/>: a double-clicked button
/// or a tab-close-after-exit lands the room in the same place without
/// re-broadcasting.
/// </summary>
public sealed class ExitRoomHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IGameModuleRegistry _games;
    private readonly IClock _clock;
    private readonly IRateLimiter _rateLimiter;

    public ExitRoomHandler(
        IRoomRepository rooms,
        IGameModuleRegistry games,
        IClock clock,
        IRateLimiter rateLimiter)
    {
        _rooms = rooms;
        _games = games;
        _clock = clock;
        _rateLimiter = rateLimiter;
    }

    public async Task<AppResult<ExitRoomResult>> HandleAsync(
        ExitRoomCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (!RoomCode.TryCreate(cmd.RoomCode, out var code))
        {
            return AppResult<ExitRoomResult>.Fail(PlatformErrors.RoomNotFound);
        }

        if (!await _rateLimiter.TryAcquireAsync(
                SessionRateLimitPolicies.ExitRoom, cmd.CallerPlayerId, ct))
        {
            return AppResult<ExitRoomResult>.Fail(PlatformErrors.RateExceeded);
        }

        try
        {
            return await _rooms.WithLockAsync(code, async () =>
            {
                var room = await _rooms.LoadAsync(code, ct);
                if (room is null)
                {
                    return AppResult<ExitRoomResult>.Fail(PlatformErrors.RoomNotFound);
                }

                var stored = room.PlayerFor(cmd.CallerRole);
                if (stored is null || stored.Id.Value != cmd.CallerPlayerId)
                {
                    return AppResult<ExitRoomResult>.Fail(PlatformErrors.SessionUnauthorized);
                }

                // Closed-idempotency and Ended/AwaitingRematch transition both
                // live on the domain method; non-exitable states return false
                // and surface as a clean error key here.
                if (!room.TryExit(out var transitioned))
                {
                    return AppResult<ExitRoomResult>.Fail(PlatformErrors.ExitNotAllowed);
                }

                if (transitioned)
                {
                    await _rooms.SaveAsync(room, ct);
                }

                return AppResult<ExitRoomResult>.Ok(new ExitRoomResult(
                    Room: RoomMapper.ToDto(room, _clock.UtcNow, _games),
                    Transitioned: transitioned));
            }, ct);
        }
        catch (LockTimeoutException)
        {
            return AppResult<ExitRoomResult>.Fail(PlatformErrors.RoomBusy);
        }
    }
}
