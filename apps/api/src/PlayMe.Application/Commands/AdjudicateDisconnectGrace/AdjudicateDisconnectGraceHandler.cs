using Microsoft.Extensions.Logging;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Errors;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.AdjudicateDisconnectGrace;

/// <summary>
/// Sprint 2 stub: the room lock is already held by the sweeper; here we
/// just record that a player failed to reconnect within the grace window.
/// Sprint 5 will replace the body with the <c>OpponentAbandoned</c> emit
/// and the <c>ClaimVictory</c> unlock — see Sprint5.TODO below.
/// </summary>
public sealed partial class AdjudicateDisconnectGraceHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IRoomCodeRedactor _redactor;
    private readonly ILogger<AdjudicateDisconnectGraceHandler> _log;

    public AdjudicateDisconnectGraceHandler(
        IRoomRepository rooms,
        IRoomCodeRedactor redactor,
        ILogger<AdjudicateDisconnectGraceHandler> log)
    {
        _rooms = rooms;
        _redactor = redactor;
        _log = log;
    }

    public async Task<AppResult<bool>> HandleAsync(
        AdjudicateDisconnectGraceCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        RoomCode code;
        try { code = new RoomCode(cmd.RoomCode); }
        catch (ArgumentException)
        {
            return AppResult<bool>.Fail(PlatformErrors.RoomNotFound);
        }

        var room = await _rooms.LoadAsync(code, ct);
        if (room is null) return AppResult<bool>.Ok(false);

        if (room.Status != RoomStatus.InProgress) return AppResult<bool>.Ok(false);

        var connected = cmd.Role switch
        {
            Role.Host => room.HostConnected,
            Role.Challenger => room.ChallengerConnected,
            _ => true,
        };
        if (connected) return AppResult<bool>.Ok(false);

        // Sprint5.TODO: emit OpponentAbandoned to the still-present player
        // and flip a flag so their UI unlocks ClaimVictory.
        // Redact the room code before logging (docs/security.md §8).
        // Pre-computing the string keeps CA1873 happy; the SHA-256 cost
        // is negligible on this cold (per-grace-elapsed) path.
        var roomRef = _redactor.Redact(cmd.RoomCode);
        LogGraceElapsed(_log, roomRef, cmd.Role);

        return AppResult<bool>.Ok(true);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Disconnect grace elapsed for room {RoomRef} role {Role} — Sprint 5 will emit OpponentAbandoned")]
    private static partial void LogGraceElapsed(ILogger logger, string roomRef, Role role);
}
