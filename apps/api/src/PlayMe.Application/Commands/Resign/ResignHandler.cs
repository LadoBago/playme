using PlayMe.Application.Abstractions;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Application.RateLimiting;
using PlayMe.Application.Telemetry;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.Resign;

/// <summary>
/// Resignation pipeline (docs/platform.md §1 #8). Mirrors the
/// move pipeline's authorization + lock + stale-clock conversion structure,
/// but doesn't consult any per-game module — resign is a platform-level
/// outcome, not a rules-engine decision.
///
/// The handler is idempotent in the sense that a call against an
/// already-<c>Ended</c> room returns <see cref="PlatformErrors.MoveMatchNotInProgress"/>
/// (the same key the move pipeline uses for "match has already ended"),
/// not a hard exception — double-clicking the confirm button before the
/// MatchEnded broadcast lands is benign.
/// </summary>
public sealed class ResignHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IGameModuleRegistry _games;
    private readonly IClock _clock;
    private readonly IClockService _clockService;
    private readonly ITimeoutScheduler _timeouts;
    private readonly IRateLimiter _rateLimiter;
    private readonly IAnalyticsClient _analytics;

    public ResignHandler(
        IRoomRepository rooms,
        IGameModuleRegistry games,
        IClock clock,
        IClockService clockService,
        ITimeoutScheduler timeouts,
        IRateLimiter rateLimiter,
        IAnalyticsClient analytics)
    {
        _rooms = rooms;
        _games = games;
        _clock = clock;
        _clockService = clockService;
        _timeouts = timeouts;
        _rateLimiter = rateLimiter;
        _analytics = analytics;
    }

    public async Task<AppResult<ResignResult>> HandleAsync(
        ResignCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (!RoomCode.TryCreate(cmd.RoomCode, out var code))
        {
            return AppResult<ResignResult>.Fail(PlatformErrors.RoomNotFound);
        }

        // Per-session rate limit before acquiring the room lock. The
        // resign confirm button can fire twice on a slow tap; the limit
        // turns the second call into a clean RateExceeded rather than
        // letting it stack against the lock.
        if (!await _rateLimiter.TryAcquireAsync(
                SessionRateLimitPolicies.Resign, cmd.CallerPlayerId, ct))
        {
            return AppResult<ResignResult>.Fail(PlatformErrors.RateExceeded);
        }

        try
        {
            return await _rooms.WithLockAsync(code, async () =>
            {
                var room = await _rooms.LoadAsync(code, ct);
                if (room is null)
                {
                    return AppResult<ResignResult>.Fail(PlatformErrors.RoomNotFound);
                }

                var stored = room.PlayerFor(cmd.CallerRole);
                if (stored is null || stored.Id.Value != cmd.CallerPlayerId)
                {
                    return AppResult<ResignResult>.Fail(PlatformErrors.SessionUnauthorized);
                }

                if (room.Status != RoomStatus.InProgress || room.CurrentMatch is null)
                {
                    return AppResult<ResignResult>.Fail(PlatformErrors.MoveMatchNotInProgress);
                }

                var match = room.CurrentMatch;
                if (match.IsEnded)
                {
                    return AppResult<ResignResult>.Fail(PlatformErrors.MoveMatchNotInProgress);
                }

                var now = _clock.UtcNow;

                // Stale-clock conversion: if the active player's effective
                // clock has already run out at this moment, the match is
                // really a timeout — yield to that outcome regardless of
                // who called resign. Mirrors SubmitMoveHandler.
                if (_clockService.HasActivePlayerTimedOut(match.Clock, now))
                {
                    match.ApplyTimeout(match.SideToMove, now);
                    room.EndCurrentMatch();
                    await _rooms.SaveAsync(room, ct);
                    await _timeouts.CancelAsync(code, ct);
                    await _analytics.TrackAsync(
                        AnalyticsEvents.MatchEnded,
                        room.Code.Value,
                        AnalyticsEvents.MatchEndedProperties(room.GameId.Value, match.Outcome!),
                        ct);

                    return AppResult<ResignResult>.Ok(new ResignResult(
                        Room: RoomMapper.ToDto(room, now, _games),
                        TimedOut: true));
                }

                var callerSide = stored.Side
                    ?? throw new InvalidOperationException(
                        "Caller has no side resolved — TryStartMatch should have rejected this room earlier.");

                match.Resign(callerSide);
                room.EndCurrentMatch();
                await _rooms.SaveAsync(room, ct);
                await _timeouts.CancelAsync(code, ct);
                await _analytics.TrackAsync(
                    AnalyticsEvents.MatchEnded,
                    room.Code.Value,
                    AnalyticsEvents.MatchEndedProperties(room.GameId.Value, match.Outcome!),
                    ct);

                return AppResult<ResignResult>.Ok(new ResignResult(
                    Room: RoomMapper.ToDto(room, now, _games),
                    TimedOut: false));
            }, ct);
        }
        catch (LockTimeoutException)
        {
            return AppResult<ResignResult>.Fail(PlatformErrors.RoomBusy);
        }
    }
}
