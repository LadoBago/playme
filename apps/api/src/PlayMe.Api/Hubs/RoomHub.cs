using Microsoft.AspNetCore.SignalR;

namespace PlayMe.Api.Hubs;

/// <summary>
/// Single SignalR hub for all room-scoped real-time operations
/// (CLAUDE.md §2.4 RoomHub method index).
///
/// Sprint 0 placeholder — only the connection lifecycle is wired so the
/// backplane round-trips and clients can negotiate the socket. Concrete
/// methods (JoinRoom / SubmitMove / Resign / ClaimVictory / OfferRematch
/// / AcceptRematch / RejectRematch / ExitRoom) land in Sprints 1-5.
/// </summary>
public sealed class RoomHub : Hub
{
    public override Task OnConnectedAsync() => base.OnConnectedAsync();

    public override Task OnDisconnectedAsync(Exception? exception) =>
        base.OnDisconnectedAsync(exception);
}
