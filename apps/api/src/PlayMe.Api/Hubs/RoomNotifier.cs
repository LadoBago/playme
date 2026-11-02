using Microsoft.AspNetCore.SignalR;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Dtos;
using PlayMe.Application.Mapping;
using PlayMe.Domain.Platform;

namespace PlayMe.Api.Hubs;

/// <summary>
/// API-side implementation of <see cref="IRoomNotifier"/>. Sweepers in
/// Infrastructure broadcast through this port; the SignalR group names
/// are shared with <see cref="RoomHub"/> so a single source of truth
/// governs which connections receive room events. Room-bearing events
/// go through the same per-viewer projection the Hub applies for
/// hidden-state games (docs/security.md §4).
/// </summary>
public sealed class RoomNotifier : IRoomNotifier
{
    private readonly IHubContext<RoomHub> _hub;
    private readonly IGameModuleRegistry _games;

    public RoomNotifier(IHubContext<RoomHub> hub, IGameModuleRegistry games)
    {
        _hub = hub;
        _games = games;
    }

    public Task BroadcastMatchEndedAsync(RoomCode code, RoomDto room, CancellationToken ct) =>
        SendToRoomAsync(RoomHubEvents.MatchEnded, code, room, r => new { room = r }, ct);

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
        RoomDto room,
        CancellationToken ct) =>
        // The exiting connection is already gone from the group by the
        // time the sweeper fires (10 s after disconnect), so Group() and
        // OthersInGroup() reach the same audience — the still-connected
        // player. Payload shape mirrors RoomHub.ExitRoom() so the client
        // doesn't have to branch on who fired the event.
        SendToRoomAsync(RoomHubEvents.OpponentExited, code, room, r => new { role = exitedRole, room = r }, ct);

    public Task BroadcastMoveAcceptedAsync(RoomCode code, RoomDto room, CancellationToken ct) =>
        SendToRoomAsync(RoomHubEvents.MoveAccepted, code, room, r => new { room = r }, ct);

    public Task BroadcastMatchStartedAsync(RoomCode code, RoomDto room, CancellationToken ct) =>
        SendToRoomAsync(RoomHubEvents.MatchStarted, code, room, r => new { room = r }, ct);

    public Task BroadcastOpponentSetupCommittedAsync(
        RoomCode code,
        Role committerRole,
        RoomDto room,
        CancellationToken ct)
    {
        // Readiness goes to the other seat only, with that seat's view —
        // the same audience the Hub's SendToOpponentAsync reaches.
        var receiver = committerRole == Role.Host ? Role.Challenger : Role.Host;
        return _hub.Clients
            .Group(RoomHub.RoleGroupName(code.Value, receiver))
            .SendAsync(
                RoomHubEvents.OpponentSetupCommitted,
                new { role = committerRole, room = RoomViewProjector.ForViewer(room, receiver, _games) },
                ct);
    }

    /// <summary>
    /// Mirror of <c>RoomHub.SendToRoomAsync</c>: one payload to the room
    /// group for perfect-information games, one per-role projection
    /// otherwise.
    /// </summary>
    private Task SendToRoomAsync(
        string evt, RoomCode code, RoomDto room, Func<RoomDto, object> payload, CancellationToken ct)
    {
        if (!RoomViewProjector.RequiresProjection(room, _games))
        {
            return _hub.Clients.Group(RoomHub.GroupName(code.Value)).SendAsync(evt, payload(room), ct);
        }

        return Task.WhenAll(
            _hub.Clients.Group(RoomHub.RoleGroupName(code.Value, Role.Host))
                .SendAsync(evt, payload(RoomViewProjector.ForViewer(room, Role.Host, _games)), ct),
            _hub.Clients.Group(RoomHub.RoleGroupName(code.Value, Role.Challenger))
                .SendAsync(evt, payload(RoomViewProjector.ForViewer(room, Role.Challenger, _games)), ct));
    }
}
