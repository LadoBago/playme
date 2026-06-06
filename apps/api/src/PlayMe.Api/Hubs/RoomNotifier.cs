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

    public Task BroadcastRoomExpiredAsync(
        RoomCode code,
        RoomExpiryReason reason,
        CancellationToken ct)
    {
        // Wire values are the Zod enum on the web side
        // (packages/shared/src/realtime/schemas.ts) — keep in sync.
        var wireReason = reason switch
        {
            RoomExpiryReason.Unjoined => "unjoined",
            RoomExpiryReason.SetupTimeout => "setupTimeout",
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
        };
        return _hub.Clients
            .Group(RoomHub.GroupName(code.Value))
            .SendAsync(RoomHubEvents.RoomExpired, new { reason = wireReason }, ct);
    }

    public Task BroadcastOpponentExitedAsync(
        RoomCode code,
        Role exitedRole,
        Application.Dtos.RoomDto room,
        CancellationToken ct) =>
        // The exiting connection is already gone from the group by the
        // time the sweeper fires (10 s after disconnect), so Group() and
        // OthersInGroup() reach the same audience — the still-connected
        // player. Payload shape mirrors RoomHub.ExitRoom() so the client
        // doesn't have to branch on who fired the event.
        _hub.Clients
            .Group(RoomHub.GroupName(code.Value))
            .SendAsync(RoomHubEvents.OpponentExited, new { role = exitedRole, room }, ct);
}
