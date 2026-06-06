using Microsoft.Extensions.Logging;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Application.Telemetry;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.AdjudicateSetupTimeout;

/// <summary>
/// Setup-deadline adjudication (Sprint 10 seam C; docs/games/seabattle.md).
/// Called by the setup-deadline sweeper inside the room lock — so it does
/// <em>not</em> wrap in <c>WithLockAsync</c> again (same convention as the
/// other adjudicators). Re-verifies the room is still
/// <see cref="RoomStatus.SettingUp"/> under the lock, then expires it
/// (<see cref="RoomStatus.Expired"/>, terminal) regardless of who
/// committed: the match never started, so setup expiry never awards a
/// win or touches the scoreboard — no forfeit, no outcome. Stale entries
/// (setup completed, match already ended, room reaped) drop silently.
/// </summary>
public sealed partial class AdjudicateSetupTimeoutHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IGameModuleRegistry _games;
    private readonly IClock _clock;
    private readonly IRoomCodeRedactor _redactor;
    private readonly IAnalyticsClient _analytics;
    private readonly ILogger<AdjudicateSetupTimeoutHandler> _log;

    public AdjudicateSetupTimeoutHandler(
        IRoomRepository rooms,
        IGameModuleRegistry games,
        IClock clock,
        IRoomCodeRedactor redactor,
        IAnalyticsClient analytics,
        ILogger<AdjudicateSetupTimeoutHandler> log)
    {
        _rooms = rooms;
        _games = games;
        _clock = clock;
        _redactor = redactor;
        _analytics = analytics;
        _log = log;
    }

    public async Task<AppResult<AdjudicateSetupTimeoutResult>> HandleAsync(
        AdjudicateSetupTimeoutCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (!RoomCode.TryCreate(cmd.RoomCode, out var code))
        {
            return AppResult<AdjudicateSetupTimeoutResult>.Fail(PlatformErrors.RoomNotFound);
        }

        var room = await _rooms.LoadAsync(code, ct);
        if (room is null)
        {
            return Dropped();
        }

        if (room.Status != RoomStatus.SettingUp
            || room.CurrentMatch is null
            || room.CurrentMatch.IsEnded)
        {
            var roomRef = _redactor.Redact(room.Code.Value);
            LogSetupDeadlineDropped(_log, roomRef, "status");
            return Dropped();
        }

        var now = _clock.UtcNow;

        // Expire regardless of who committed: the match never started, so
        // the deadline never awards a win — a one-sided commit is not a
        // forfeit (platform decision; docs/state.md §2.1 `SettingUp`).
        // No grace cleanup needed: setup-phase disconnects schedule none.
        room.ExpireSetup();
        await _rooms.SaveAsync(room, ct);
        await _analytics.TrackAsync(
            AnalyticsEvents.RoomExpired,
            room.Code.Value,
            AnalyticsEvents.RoomExpiredProperties(room.GameId.Value),
            ct);

        return AppResult<AdjudicateSetupTimeoutResult>.Ok(
            new AdjudicateSetupTimeoutResult(
                Room: RoomMapper.ToDto(room, now, _games),
                Expired: true));
    }

    private static AppResult<AdjudicateSetupTimeoutResult> Dropped() =>
        AppResult<AdjudicateSetupTimeoutResult>.Ok(
            new AdjudicateSetupTimeoutResult(Room: null, Expired: false));

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Setup deadline dropped for room {RoomRef}: {Reason}")]
    private static partial void LogSetupDeadlineDropped(
        ILogger logger, string roomRef, string reason);
}
