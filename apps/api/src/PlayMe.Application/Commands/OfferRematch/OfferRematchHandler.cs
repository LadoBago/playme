using PlayMe.Application.Abstractions;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Application.RateLimiting;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.OfferRematch;

/// <summary>
/// First step of the rematch handshake (docs/platform.md §1 #10).
/// Under the room lock:
/// <list type="bullet">
///   <item>From <c>Ended</c>: record the caller as the offerer and flip the
///   room to <c>AwaitingRematch</c>; hub emits <c>RematchOffered</c>.</item>
///   <item>From <c>AwaitingRematch</c> with the opposite caller: treat as
///   implicit accept, swap sides, start a new match; hub emits
///   <c>MatchStarted</c>.</item>
///   <item>Anything else (including a duplicate offer from the same caller)
///   → <c>RematchInvalidState</c>.</item>
/// </list>
/// </summary>
public sealed class OfferRematchHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IGameModuleRegistry _games;
    private readonly IClock _clock;
    private readonly ITimeoutScheduler _timeouts;
    private readonly IRateLimiter _rateLimiter;

    public OfferRematchHandler(
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

    public async Task<AppResult<OfferRematchHandlerResult>> HandleAsync(
        OfferRematchCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (!RoomCode.TryCreate(cmd.RoomCode, out var code))
        {
            return AppResult<OfferRematchHandlerResult>.Fail(PlatformErrors.RoomNotFound);
        }

        if (!await _rateLimiter.TryAcquireAsync(
                SessionRateLimitPolicies.Rematch, cmd.CallerPlayerId, ct))
        {
            return AppResult<OfferRematchHandlerResult>.Fail(PlatformErrors.RateExceeded);
        }

        try
        {
            return await _rooms.WithLockAsync(code, async () =>
            {
                var room = await _rooms.LoadAsync(code, ct);
                if (room is null)
                {
                    return AppResult<OfferRematchHandlerResult>.Fail(PlatformErrors.RoomNotFound);
                }

                var stored = room.PlayerFor(cmd.CallerRole);
                if (stored is null || stored.Id.Value != cmd.CallerPlayerId)
                {
                    return AppResult<OfferRematchHandlerResult>.Fail(PlatformErrors.SessionUnauthorized);
                }

                var module = _games.GetModule(room.GameId);
                var now = _clock.UtcNow;

                if (!room.TryOfferRematch(cmd.CallerRole, module, now, out var effect))
                {
                    return AppResult<OfferRematchHandlerResult>.Fail(PlatformErrors.RematchInvalidState);
                }

                await _rooms.SaveAsync(room, ct);

                // Implicit accept transitioned the room into a fresh match —
                // schedule the active player's timeout. The OfferRecorded
                // path keeps the room post-Ended; no clock is running yet.
                if (effect == RematchOfferResult.ImplicitlyAccepted && room.CurrentMatch is not null)
                {
                    await _timeouts.ScheduleAsync(
                        code,
                        room.CurrentMatch.Clock.ActivePlayerDeadline(),
                        ct);
                }

                return AppResult<OfferRematchHandlerResult>.Ok(new OfferRematchHandlerResult(
                    Room: RoomMapper.ToDto(room, now, _games),
                    Effect: effect));
            }, ct);
        }
        catch (LockTimeoutException)
        {
            return AppResult<OfferRematchHandlerResult>.Fail(PlatformErrors.RoomBusy);
        }
    }
}
