using PlayMe.Application.Dtos;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Abstractions;

/// <summary>
/// Broadcast hook for server-emitted room events that originate
/// <em>outside</em> a Hub method call — namely the BackgroundService
/// sweepers in Infrastructure that adjudicate scheduled timeouts,
/// disconnect-grace deadlines, and unjoined-room expiry. Implementation
/// in the API layer wraps <c>IHubContext&lt;RoomHub&gt;</c>; defining
/// the port here keeps Application/Infrastructure free of a direct
/// SignalR dependency (CLAUDE.md §2.4 dependency rule).
/// </summary>
public interface IRoomNotifier
{
    /// <summary>
    /// Broadcast <c>MatchEnded</c> to every connection in the room group.
    /// The room state in <paramref name="room"/> reflects the post-
    /// adjudication snapshot (Status == Ended, CurrentMatch.Outcome set).
    /// </summary>
    Task BroadcastMatchEndedAsync(RoomCode code, RoomDto room, CancellationToken ct);

    /// <summary>
    /// Broadcast <c>RoomExpired</c> to every connection in the room
    /// group. Fired by the room-expiry sweeper when a
    /// <see cref="RoomStatus.WaitingForOpponent"/> room reaches its
    /// 30-minute deadline without anyone joining
    /// (<see cref="RoomExpiryReason.Unjoined"/>), and by the
    /// setup-deadline sweeper when neither player commits setup in
    /// time (<see cref="RoomExpiryReason.SetupTimeout"/>). The
    /// <paramref name="reason"/> rides on the payload so the UI can
    /// explain which deadline actually fired instead of showing the
    /// unjoined copy for both (docs/state.md §2.3).
    /// </summary>
    Task BroadcastRoomExpiredAsync(RoomCode code, RoomExpiryReason reason, CancellationToken ct);

    /// <summary>
    /// Broadcast <c>OpponentExited</c> to the room group from outside a
    /// Hub call. Used by the post-match-exit grace sweeper when the
    /// reconnect window elapses without the disconnected player
    /// returning (docs/state.md §2.4). <paramref name="exitedRole"/> is
    /// the role of the leaving party; the still-connected player picks
    /// it up to render "opponent left".
    /// </summary>
    Task BroadcastOpponentExitedAsync(
        RoomCode code,
        Role exitedRole,
        RoomDto room,
        CancellationToken ct);
}
