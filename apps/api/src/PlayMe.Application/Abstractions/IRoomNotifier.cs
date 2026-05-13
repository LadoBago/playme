using PlayMe.Application.Dtos;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Abstractions;

/// <summary>
/// Broadcast hook for server-emitted room events that originate
/// <em>outside</em> a Hub method call — namely the BackgroundService
/// sweepers in Infrastructure that adjudicate scheduled timeouts and
/// disconnect-grace deadlines. Implementation in the API layer wraps
/// <c>IHubContext&lt;RoomHub&gt;</c>; defining the port here keeps
/// Application/Infrastructure free of a direct SignalR dependency
/// (CLAUDE.md §2.4 dependency rule).
/// </summary>
public interface IRoomNotifier
{
    /// <summary>
    /// Broadcast <c>MatchEnded</c> to every connection in the room group.
    /// The room state in <paramref name="room"/> reflects the post-
    /// adjudication snapshot (Status == Ended, CurrentMatch.Outcome set).
    /// </summary>
    Task BroadcastMatchEndedAsync(RoomCode code, RoomDto room, CancellationToken ct);
}
