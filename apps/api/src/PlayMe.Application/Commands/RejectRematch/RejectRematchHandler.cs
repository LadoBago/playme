using PlayMe.Application.Abstractions;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Application.RateLimiting;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.RejectRematch;

/// <summary>
/// Responder-side reject of the current rematch offer
/// (docs/platform-and-games.md §1 #10). Closes the room; the hub broadcasts
/// <c>RematchDeclined</c> to the still-present offerer.
/// </summary>
public sealed class RejectRematchHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IGameModuleRegistry _games;
    private readonly IClock _clock;
    private readonly IRateLimiter _rateLimiter;

    public RejectRematchHandler(
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

    public async Task<AppResult<RejectRematchResult>> HandleAsync(
        RejectRematchCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        RoomCode code;
        try { code = new RoomCode(cmd.RoomCode); }
        catch (ArgumentException)
        {
            return AppResult<RejectRematchResult>.Fail(PlatformErrors.RoomNotFound);
        }

        if (!await _rateLimiter.TryAcquireAsync(
                SessionRateLimitPolicies.Rematch, cmd.CallerPlayerId, ct))
        {
            return AppResult<RejectRematchResult>.Fail(PlatformErrors.RateExceeded);
        }

        try
        {
            return await _rooms.WithLockAsync(code, async () =>
            {
                var room = await _rooms.LoadAsync(code, ct);
                if (room is null)
                {
                    return AppResult<RejectRematchResult>.Fail(PlatformErrors.RoomNotFound);
                }

                var stored = room.PlayerFor(cmd.CallerRole);
                if (stored is null || stored.Id.Value != cmd.CallerPlayerId)
                {
                    return AppResult<RejectRematchResult>.Fail(PlatformErrors.SessionUnauthorized);
                }

                if (room.Status != RoomStatus.AwaitingRematch)
                {
                    return AppResult<RejectRematchResult>.Fail(PlatformErrors.RematchInvalidState);
                }
                if (room.RematchOffererRole == cmd.CallerRole)
                {
                    return AppResult<RejectRematchResult>.Fail(PlatformErrors.RematchNotResponder);
                }

                room.RejectRematch(cmd.CallerRole);
                await _rooms.SaveAsync(room, ct);

                return AppResult<RejectRematchResult>.Ok(new RejectRematchResult(
                    Room: RoomMapper.ToDto(room, _clock.UtcNow, _games)));
            }, ct);
        }
        catch (LockTimeoutException)
        {
            return AppResult<RejectRematchResult>.Fail(PlatformErrors.RoomBusy);
        }
    }
}
