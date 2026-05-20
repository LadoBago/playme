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
    /// group. Fired by the expiry sweeper when a
    /// <see cref="RoomStatus.WaitingForOpponent"/> room reaches its
    /// 30-minute deadline without anyone joining. The host may still
    /// be on the share-link page; this event lets the UI render a
    /// clean "this room has expired" state instead of a generic
    /// disconnect error.
    /// </summary>
    Task BroadcastRoomExpiredAsync(RoomCode code, CancellationToken ct);
}
