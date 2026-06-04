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
/// <see cref="RoomStatus.SettingUp"/> under the lock, then:
/// <list type="bullet">
///   <item>exactly one side uncommitted → that side forfeits with
///   <see cref="Timeout"/> (rolls into the opponent's scoreboard win like
///   every clock-family outcome); the room goes to Ended and the rematch
///   flow stays available;</item>
///   <item>neither side committed → the room expires
///   (<see cref="RoomStatus.Expired"/>, terminal): both players walked
///   away mid-handshake, there is no one to award a win to;</item>
///   <item>anything else (setup completed, match already ended, room
///   reaped) → drop silently; the entry is stale.</item>
/// </list>
/// </summary>
public sealed partial class AdjudicateSetupTimeoutHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IGameModuleRegistry _games;
    private readonly IClock _clock;
    private readonly IDisconnectGraceScheduler _graces;
    private readonly IRoomCodeRedactor _redactor;
    private readonly IAnalyticsClient _analytics;
    private readonly ILogger<AdjudicateSetupTimeoutHandler> _log;

    public AdjudicateSetupTimeoutHandler(
        IRoomRepository rooms,
        IGameModuleRegistry games,
        IClock clock,
        IDisconnectGraceScheduler graces,
        IRoomCodeRedactor redactor,
        IAnalyticsClient analytics,
        ILogger<AdjudicateSetupTimeoutHandler> log)
    {
        _rooms = rooms;
        _games = games;
        _clock = clock;
        _graces = graces;
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

        var match = room.CurrentMatch;
        var now = _clock.UtcNow;

        if (!match.HostSetupCommitted && !match.ChallengerSetupCommitted)
        {
            // Neither side committed — both walked away mid-handshake.
            // Terminal expiry, no match outcome, scoreboard discarded with
            // the room (accepted v1 trade-off; see the sprint plan).
            room.ExpireSetup();
            await _rooms.SaveAsync(room, ct);
            await _graces.CancelAsync(code, Role.Host, ct);
            await _graces.CancelAsync(code, Role.Challenger, ct);
            await _analytics.TrackAsync(
                AnalyticsEvents.RoomExpired,
                room.Code.Value,
                AnalyticsEvents.RoomExpiredProperties(room.GameId.Value),
                ct);

            return AppResult<AdjudicateSetupTimeoutResult>.Ok(
                new AdjudicateSetupTimeoutResult(
                    Room: RoomMapper.ToDto(room, now, _games),
                    MatchEnded: false,
                    Expired: true));
        }

        // Exactly one side uncommitted — they forfeit on the setup clock.
        var forfeitRole = match.HostSetupCommitted ? Role.Challenger : Role.Host;
        var forfeitSide = room.SideFor(forfeitRole)
            ?? throw new InvalidOperationException(
                "Forfeiting role has no side resolved — TryStartMatch should have rejected this room.");

        match.EndDuringSetup(new Domain.Platform.Timeout(forfeitSide));
        room.EndCurrentMatch();
        await _rooms.SaveAsync(room, ct);
        await _graces.CancelAsync(code, Role.Host, ct);
        await _graces.CancelAsync(code, Role.Challenger, ct);
        await _analytics.TrackAsync(
            AnalyticsEvents.MatchEnded,
            room.Code.Value,
            AnalyticsEvents.MatchEndedProperties(room.GameId.Value, match.Outcome!),
            ct);

        return AppResult<AdjudicateSetupTimeoutResult>.Ok(
            new AdjudicateSetupTimeoutResult(
                Room: RoomMapper.ToDto(room, now, _games),
                MatchEnded: true,
                Expired: false));
    }

    private static AppResult<AdjudicateSetupTimeoutResult> Dropped() =>
        AppResult<AdjudicateSetupTimeoutResult>.Ok(
            new AdjudicateSetupTimeoutResult(Room: null, MatchEnded: false, Expired: false));

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Setup deadline dropped for room {RoomRef}: {Reason}")]
    private static partial void LogSetupDeadlineDropped(
        ILogger logger, string roomRef, string reason);
}
