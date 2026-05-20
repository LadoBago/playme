using Microsoft.AspNetCore.SignalR;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Dtos;
using PlayMe.Domain.Platform;

namespace PlayMe.Api.Hubs;

/// <summary>
/// API-side implementation of <see cref="IRoomNotifier"/>. Sweepers in
/// Infrastructure broadcast through this port; the SignalR group name
/// is shared with <see cref="RoomHub.GroupName"/> so a single source of
/// truth governs which connections receive room events.
/// </summary>
public sealed class RoomNotifier : IRoomNotifier
{
    private readonly IHubContext<RoomHub> _hub;

    public RoomNotifier(IHubContext<RoomHub> hub)
    {
        _hub = hub;
    }

    public Task BroadcastMatchEndedAsync(RoomCode code, RoomDto room, CancellationToken ct) =>
        _hub.Clients
            .Group(RoomHub.GroupName(code.Value))
            .SendAsync(RoomHubEvents.MatchEnded, new { room }, ct);

    public Task BroadcastRoomExpiredAsync(RoomCode code, CancellationToken ct) =>
        // No payload — the event itself is the signal that the room is
        // gone. Adding fields here later (e.g. an `expiredAt` for client
        // analytics) is forward-compatible since the web parses with
        // Zod and ignores unknown keys.
        _hub.Clients
            .Group(RoomHub.GroupName(code.Value))
            .SendAsync(RoomHubEvents.RoomExpired, new { }, ct);
}
