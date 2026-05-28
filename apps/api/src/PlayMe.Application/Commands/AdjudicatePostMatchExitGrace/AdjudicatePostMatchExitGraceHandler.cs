using Microsoft.Extensions.Logging;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.AdjudicatePostMatchExitGrace;

/// <summary>
/// Post-match reconnect-grace adjudication (docs/state.md §2.4). Called
/// by the post-match-exit sweeper inside the room lock when a scheduled
/// grace entry has reached its deadline. Re-verifies every precondition
/// — a reconnect, an explicit exit by the other player, or a room reap
/// since the entry was scheduled can have invalidated it — and if they
/// all hold, transitions the room to <see cref="RoomStatus.Closed"/>.
///
/// Returns Exited=true when the call closed the room (the sweeper then
/// broadcasts <c>OpponentExited</c> using the returned DTO); Exited=false
/// on any short-circuit (room gone, room already Closed, role
/// reconnected).
/// </summary>
public sealed partial class AdjudicatePostMatchExitGraceHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IGameModuleRegistry _games;
    private readonly IClock _clock;
    private readonly IRoomCodeRedactor _redactor;
    private readonly ILogger<AdjudicatePostMatchExitGraceHandler> _log;

    public AdjudicatePostMatchExitGraceHandler(
        IRoomRepository rooms,
        IGameModuleRegistry games,
        IClock clock,
        IRoomCodeRedactor redactor,
        ILogger<AdjudicatePostMatchExitGraceHandler> log)
    {
        _rooms = rooms;
        _games = games;
        _clock = clock;
        _redactor = redactor;
        _log = log;
    }

    public async Task<AppResult<AdjudicatePostMatchExitGraceResult>> HandleAsync(
        AdjudicatePostMatchExitGraceCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (!RoomCode.TryCreate(cmd.RoomCode, out var code))
        {
            return AppResult<AdjudicatePostMatchExitGraceResult>.Fail(PlatformErrors.RoomNotFound);
        }

        var room = await _rooms.LoadAsync(code, ct);
        if (room is null)
        {
            return AppResult<AdjudicatePostMatchExitGraceResult>.Ok(
                new AdjudicatePostMatchExitGraceResult(Room: null, Exited: false, ExitedRole: cmd.Role));
        }

        // Re-verify under the room lock (already held by the caller — the
        // sweeper acquires it before dispatching). Any of these failing
        // means the scheduled entry was invalidated by a later event:
        //  - the room must still be in a post-match state (the other
        //    player may have explicitly exited first, closing the room);
        //  - the role must still be marked disconnected (reconnect cancels
        //    via IPostMatchExitGraceScheduler.CancelAsync, but a race can
        //    leave a stale entry — the defensive re-check covers it).
        if (room.Status is not (RoomStatus.Ended or RoomStatus.AwaitingRematch))
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

        // Preconditions above guarantee TryExit succeeds with a transition.
        room.TryExit(out _);
        await _rooms.SaveAsync(room, ct);

        return AppResult<AdjudicatePostMatchExitGraceResult>.Ok(
            new AdjudicatePostMatchExitGraceResult(
                Room: RoomMapper.ToDto(room, _clock.UtcNow, _games),
                Exited: true,
                ExitedRole: cmd.Role));
    }

    private AppResult<AdjudicatePostMatchExitGraceResult> Drop(Room room, Role role, string reason)
    {
        var roomRef = _redactor.Redact(room.Code.Value);
        LogGraceDropped(_log, roomRef, role, reason);
        return AppResult<AdjudicatePostMatchExitGraceResult>.Ok(
            new AdjudicatePostMatchExitGraceResult(Room: null, Exited: false, ExitedRole: role));
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Post-match exit grace dropped for room {RoomRef} role {Role}: {Reason}")]
    private static partial void LogGraceDropped(
        ILogger logger, string roomRef, Role role, string reason);
}
