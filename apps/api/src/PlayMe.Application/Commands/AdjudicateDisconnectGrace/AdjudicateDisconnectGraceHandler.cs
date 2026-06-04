using Microsoft.Extensions.Logging;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Application.Telemetry;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.AdjudicateDisconnectGrace;

/// <summary>
/// Reconnect-grace adjudication (docs/platform.md §1 #7). Called
/// by the grace sweeper inside the room lock when a scheduled grace entry
/// has reached its deadline. Re-verifies every precondition under the lock
/// — a reconnect or a turn-flip since the entry was scheduled can have
/// invalidated it — and if they all hold, ends the match with
/// <see cref="Disconnect"/>.
///
/// Returns true when the call ended a match (the hub broadcasts
/// <c>MatchEnded</c> for that case); false on any short-circuit (room
/// gone, status changed, reconnected, turn flipped, chess clock already
/// timed out).
/// </summary>
public sealed partial class AdjudicateDisconnectGraceHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IGameModuleRegistry _games;
    private readonly IClock _clock;
    private readonly IClockService _clockService;
    private readonly ITimeoutScheduler _timeouts;
    private readonly ISetupDeadlineScheduler _setupDeadlines;
    private readonly IRoomCodeRedactor _redactor;
    private readonly IAnalyticsClient _analytics;
    private readonly ILogger<AdjudicateDisconnectGraceHandler> _log;

    public AdjudicateDisconnectGraceHandler(
        IRoomRepository rooms,
        IGameModuleRegistry games,
        IClock clock,
        IClockService clockService,
        ITimeoutScheduler timeouts,
        ISetupDeadlineScheduler setupDeadlines,
        IRoomCodeRedactor redactor,
        IAnalyticsClient analytics,
        ILogger<AdjudicateDisconnectGraceHandler> log)
    {
        _rooms = rooms;
        _games = games;
        _clock = clock;
        _clockService = clockService;
        _timeouts = timeouts;
        _setupDeadlines = setupDeadlines;
        _redactor = redactor;
        _analytics = analytics;
        _log = log;
    }

    public async Task<AppResult<AdjudicateDisconnectGraceResult>> HandleAsync(
        AdjudicateDisconnectGraceCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (!RoomCode.TryCreate(cmd.RoomCode, out var code))
        {
            return AppResult<AdjudicateDisconnectGraceResult>.Fail(PlatformErrors.RoomNotFound);
        }

        var room = await _rooms.LoadAsync(code, ct);
        if (room is null)
        {
            return AppResult<AdjudicateDisconnectGraceResult>.Ok(
                new AdjudicateDisconnectGraceResult(Room: null, MatchEnded: false));
        }

        // Re-verify under the room lock (already held by the caller — the
        // sweeper acquires it before dispatching). Any of these checks
        // failing means the scheduled entry has been invalidated by a
        // later event (reconnect, turn flip, chess-clock timeout, setup
        // commit):
        //  - room must still be InProgress — or SettingUp (Sprint 10 seam
        //    C: setup-phase disconnects adjudicate the same way)
        //  - the role must still be marked disconnected
        //  - InProgress only: the role must still be the active player and
        //    the chess clock must not have already run out (yield to the
        //    timeout sweeper so the outcome is Timeout, not Disconnect)
        //  - SettingUp only: the role must still owe a setup commit
        if (room.Status is not (RoomStatus.InProgress or RoomStatus.SettingUp))
        {
            return Drop(room, cmd.Role, "status");
        }

        var stillConnected = cmd.Role switch
        {
            Role.Host => room.HostConnected,
            Role.Challenger => room.ChallengerConnected,
            _ => true,
        };
        if (stillConnected) return Drop(room, cmd.Role, "reconnected");

        var match = room.CurrentMatch;
        if (match is null || match.IsEnded) return Drop(room, cmd.Role, "match");

        var now = _clock.UtcNow;
        var inSetup = room.Status == RoomStatus.SettingUp;
        if (inSetup)
        {
            // A committed player owes nothing during setup; their entry —
            // if one ever existed — is stale.
            if (match.HasCommittedSetup(cmd.Role)) return Drop(room, cmd.Role, "committed");
        }
        else
        {
            if (match.Clock.ActivePlayer != cmd.Role) return Drop(room, cmd.Role, "turn");
            if (_clockService.HasActivePlayerTimedOut(match.Clock, now))
            {
                return Drop(room, cmd.Role, "timedout");
            }
        }

        var losingSide = room.SideFor(cmd.Role)
            ?? throw new InvalidOperationException(
                "Disconnected role has no side resolved — TryStartMatch should have rejected this room.");

        if (inSetup)
        {
            // The clock never started — record the outcome without
            // touching it (see Match.EndDuringSetup).
            match.EndDuringSetup(new Disconnect(losingSide));
        }
        else
        {
            match.ApplyDisconnect(losingSide, now);
        }
        room.EndCurrentMatch();
        await _rooms.SaveAsync(room, ct);
        await _timeouts.CancelAsync(code, ct);
        await _setupDeadlines.CancelAsync(code, ct);
        await _analytics.TrackAsync(
            AnalyticsEvents.MatchEnded,
            room.Code.Value,
            AnalyticsEvents.MatchEndedProperties(room.GameId.Value, match.Outcome!),
            ct);

        return AppResult<AdjudicateDisconnectGraceResult>.Ok(
            new AdjudicateDisconnectGraceResult(
                Room: RoomMapper.ToDto(room, now, _games),
                MatchEnded: true));
    }

    private AppResult<AdjudicateDisconnectGraceResult> Drop(Room room, Role role, string reason)
    {
        var roomRef = _redactor.Redact(room.Code.Value);
        LogGraceDropped(_log, roomRef, role, reason);
        return AppResult<AdjudicateDisconnectGraceResult>.Ok(
            new AdjudicateDisconnectGraceResult(Room: null, MatchEnded: false));
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Disconnect grace dropped for room {RoomRef} role {Role}: {Reason}")]
    private static partial void LogGraceDropped(
        ILogger logger, string roomRef, Role role, string reason);
}
