using PlayMe.Application.Abstractions;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Application.RateLimiting;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.AcceptRematch;

/// <summary>
/// Responder-side accept of the current rematch offer
/// (docs/platform-and-games.md §1 #10). Swaps sides and starts a fresh
/// match under the room lock; the hub broadcasts <c>MatchStarted</c>.
/// </summary>
public sealed class AcceptRematchHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IGameModuleRegistry _games;
    private readonly IClock _clock;
    private readonly ITimeoutScheduler _timeouts;
    private readonly IRateLimiter _rateLimiter;

    public AcceptRematchHandler(
        IRoomRepository rooms,
        IGameModuleRegistry games,
        IClock clock,
        ITimeoutScheduler timeouts,
        IRateLimiter rateLimiter)
    {
        _rooms = rooms;
        _games = games;
        _clock = clock;
        _timeouts = timeouts;
        _rateLimiter = rateLimiter;
    }

    public async Task<AppResult<AcceptRematchResult>> HandleAsync(
        AcceptRematchCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        RoomCode code;
        try { code = new RoomCode(cmd.RoomCode); }
        catch (ArgumentException)
        {
            return AppResult<AcceptRematchResult>.Fail(PlatformErrors.RoomNotFound);
        }

        if (!await _rateLimiter.TryAcquireAsync(
                SessionRateLimitPolicies.Rematch, cmd.CallerPlayerId, ct))
        {
            return AppResult<AcceptRematchResult>.Fail(PlatformErrors.RateExceeded);
        }

        try
        {
            return await _rooms.WithLockAsync(code, async () =>
            {
                var room = await _rooms.LoadAsync(code, ct);
                if (room is null)
                {
                    return AppResult<AcceptRematchResult>.Fail(PlatformErrors.RoomNotFound);
                }

                var stored = room.PlayerFor(cmd.CallerRole);
                if (stored is null || stored.Id.Value != cmd.CallerPlayerId)
                {
                    return AppResult<AcceptRematchResult>.Fail(PlatformErrors.SessionUnauthorized);
                }

                var module = _games.GetModule(room.GameId);
                var now = _clock.UtcNow;

                // Two distinct invariants → two distinct error keys, so the
                // web can surface the right reason. AwaitingRematch enforcement
                // lives on the room; "you can't accept your own offer" too.
                if (room.Status != RoomStatus.AwaitingRematch)
                {
                    return AppResult<AcceptRematchResult>.Fail(PlatformErrors.RematchInvalidState);
                }
                if (room.RematchOffererRole == cmd.CallerRole)
                {
                    return AppResult<AcceptRematchResult>.Fail(PlatformErrors.RematchNotResponder);
                }

                room.AcceptRematch(cmd.CallerRole, module, now);
                await _rooms.SaveAsync(room, ct);

                if (room.CurrentMatch is not null)
                {
                    await _timeouts.ScheduleAsync(
                        code,
                        room.CurrentMatch.Clock.ActivePlayerDeadline(),
                        ct);
                }

                return AppResult<AcceptRematchResult>.Ok(new AcceptRematchResult(
                    Room: RoomMapper.ToDto(room, now, _games)));
            }, ct);
        }
        catch (LockTimeoutException)
        {
            return AppResult<AcceptRematchResult>.Fail(PlatformErrors.RoomBusy);
        }
    }
}
