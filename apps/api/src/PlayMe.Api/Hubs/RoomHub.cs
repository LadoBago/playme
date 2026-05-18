using Microsoft.AspNetCore.SignalR;
using PlayMe.Api.Security;
using PlayMe.Application;
using PlayMe.Application.Commands.ExitRoom;
using PlayMe.Application.Commands.RegisterPresence;
using PlayMe.Application.Commands.ReleasePresence;
using PlayMe.Application.Commands.Resign;
using PlayMe.Application.Commands.SubmitMove;
using PlayMe.Application.Dtos;
using PlayMe.Application.Errors;

namespace PlayMe.Api.Hubs;

/// <summary>
/// Single SignalR hub for all room-scoped real-time operations
/// (CLAUDE.md §2.4). Sprint 1 wires connection lifecycle plus the two
/// active methods <see cref="JoinRoom"/> and <see cref="SubmitMove"/>;
/// resign, rematch, claim-victory, and exit arrive in later sprints.
///
/// Authorization is per CLAUDE.md §5.4: the signed session cookie is read
/// on connect, the decoded <see cref="Session"/> is cached in
/// <see cref="HubCallerContext.Items"/>, and every method dispatches with
/// the (roomCode, playerId, role) triple — the client never claims its
/// own identity.
/// </summary>
public sealed class RoomHub : Hub
{
    internal const string SessionContextKey = "playme.session";

    private readonly SessionCookieReader _sessionReader;
    private readonly RegisterPresenceHandler _registerPresence;
    private readonly ReleasePresenceHandler _releasePresence;
    private readonly SubmitMoveHandler _submitMove;
    private readonly ResignHandler _resign;
    private readonly ExitRoomHandler _exitRoom;

    public RoomHub(
        SessionCookieReader sessionReader,
        RegisterPresenceHandler registerPresence,
        ReleasePresenceHandler releasePresence,
        SubmitMoveHandler submitMove,
        ResignHandler resign,
        ExitRoomHandler exitRoom)
    {
        _sessionReader = sessionReader;
        _registerPresence = registerPresence;
        _releasePresence = releasePresence;
        _submitMove = submitMove;
        _resign = resign;
        _exitRoom = exitRoom;
    }

    public override async Task OnConnectedAsync()
    {
        // Don't reject unauthenticated connections — the room page mounts
        // SignalR before it knows whether the visitor has a session cookie
        // for this room. Aborting here surfaces as "connection stopped
        // during negotiation" in the browser console, which is noise for
        // what's an expected probe. Instead, every Hub method gates on
        // RequireSession() so unauthenticated callers get a clean
        // HubException("errors.session.unauthorized") at method-call time.
        var http = Context.GetHttpContext();
        var session = http is null ? null : _sessionReader.Read(http.Request);

        if (session is not null)
        {
            Context.Items[SessionContextKey] = session;
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(session.RoomCode.Value));
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Best-effort presence release. If the session isn't there (the
        // OnConnectedAsync abort path) there's nothing to do.
        //
        // Do NOT pass Context.ConnectionAborted here — by the time this
        // method runs, the leaving connection's token is already canceled,
        // which would (a) abort the Redis presence-release mid-flight and
        // (b) cancel the broadcast to *other* connections in the group
        // (who are still very much alive). SignalR then wraps the
        // OperationCanceledException as "Error when dispatching
        // 'OnDisconnectedAsync' on hub" in logs / Sentry.
        if (Context.Items[SessionContextKey] is Session session)
        {
            var cmd = new ReleasePresenceCommand(
                session.RoomCode.Value, session.PlayerId.Value, session.Role);
            var result = await _releasePresence.HandleAsync(cmd, CancellationToken.None);
            if (result.Succeeded)
            {
                var value = result.Value!;
                switch (value.Effect)
                {
                    case PresenceReleaseEffect.OpponentDisconnected:
                        await Clients.OthersInGroup(GroupName(session.RoomCode.Value))
                            .SendAsync(RoomHubEvents.OpponentDisconnected,
                                new { role = session.Role },
                                CancellationToken.None);
                        break;
                    case PresenceReleaseEffect.OpponentExited:
                        await Clients.OthersInGroup(GroupName(session.RoomCode.Value))
                            .SendAsync(RoomHubEvents.OpponentExited,
                                new { role = session.Role, room = value.Room },
                                CancellationToken.None);
                        break;
                    case PresenceReleaseEffect.None:
                        break;
                }
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Register the caller's presence in the room. Per §2.4 RoomHub method
    /// index, this is called on every (re)connect; the server records that
    /// the role is online and — if both players are now registered AND
    /// both connected — starts the match per §2.9.
    ///
    /// <paramref name="expectedRoomCode"/> is the room code from the URL
    /// the client is rendering. The session cookie is single-slot per
    /// browser (encrypted token carries one room's identity); a user who
    /// last played in Room A and then opens Room C's link would otherwise
    /// see Room A's state surface here. Validating the URL against the
    /// cookie's room makes the mismatch fail cleanly — the client falls
    /// back to the JoinForm and mints a fresh session for Room C.
    /// </summary>
    public async Task<RoomSessionDto> JoinRoom(string expectedRoomCode)
    {
        var session = RequireSession();
        if (!string.Equals(session.RoomCode.Value, expectedRoomCode, StringComparison.Ordinal))
        {
            // Stale cookie for a different room. Don't run RegisterPresence
            // — that would mark the caller connected in the wrong room and
            // emit OpponentReconnected to the wrong group.
            throw new HubException(PlatformErrors.SessionUnauthorized);
        }

        var cmd = new RegisterPresenceCommand(
            session.RoomCode.Value, session.PlayerId.Value, session.Role);

        var result = await _registerPresence.HandleAsync(cmd, Context.ConnectionAborted);
        if (!result.Succeeded)
        {
            throw new HubException(result.Error!);
        }

        var value = result.Value!;
        if (value.MatchJustStarted)
        {
            await Clients.Group(GroupName(session.RoomCode.Value))
                .SendAsync(RoomHubEvents.MatchStarted,
                    new { room = value.Room },
                    Context.ConnectionAborted);
        }
        else if (value.Reconnected)
        {
            await Clients.OthersInGroup(GroupName(session.RoomCode.Value))
                .SendAsync(RoomHubEvents.OpponentReconnected,
                    new { role = session.Role, room = value.Room },
                    Context.ConnectionAborted);
        }

        return new RoomSessionDto(value.CallerRole, value.Room);
    }

    /// <summary>
    /// Submit a move on behalf of the caller. The authoritative pipeline
    /// (§2.1) runs in <c>SubmitMoveHandler</c>; this method translates
    /// failures into <see cref="HubException"/> with the i18n key as the
    /// message, and on success broadcasts <c>MoveAccepted</c> and
    /// <c>MatchEnded</c> to the room group.
    /// </summary>
    public async Task<RoomDto> SubmitMove(MoveDto move)
    {
        var session = RequireSession();
        var cmd = new SubmitMoveCommand(
            session.RoomCode.Value, session.PlayerId.Value, session.Role, move);

        var result = await _submitMove.HandleAsync(cmd, Context.ConnectionAborted);
        if (!result.Succeeded)
        {
            throw new HubException(result.Error!);
        }

        var value = result.Value!;
        if (!value.TimedOut)
        {
            await Clients.Group(GroupName(session.RoomCode.Value))
                .SendAsync(RoomHubEvents.MoveAccepted,
                    new { room = value.Room },
                    Context.ConnectionAborted);
        }

        if (value.MatchEnded)
        {
            await Clients.Group(GroupName(session.RoomCode.Value))
                .SendAsync(RoomHubEvents.MatchEnded,
                    new { room = value.Room },
                    Context.ConnectionAborted);
        }

        return value.Room;
    }

    /// <summary>
    /// Voluntary in-progress concession (docs/platform-and-games.md §1 #8).
    /// The caller's confirmation step lives on the web — the server only
    /// authorizes and applies. Always broadcasts <c>MatchEnded</c>; the
    /// outcome payload distinguishes <c>resign</c> from a stale-clock
    /// <c>timeout</c> conversion (see <c>ResignHandler</c>).
    /// </summary>
    public async Task<RoomDto> Resign()
    {
        var session = RequireSession();
        var cmd = new ResignCommand(
            session.RoomCode.Value, session.PlayerId.Value, session.Role);

        var result = await _resign.HandleAsync(cmd, Context.ConnectionAborted);
        if (!result.Succeeded)
        {
            throw new HubException(result.Error!);
        }

        var value = result.Value!;
        await Clients.Group(GroupName(session.RoomCode.Value))
            .SendAsync(RoomHubEvents.MatchEnded,
                new { room = value.Room },
                Context.ConnectionAborted);

        return value.Room;
    }

    /// <summary>
    /// Voluntary post-match exit (docs/state.md §2.4). Valid in
    /// <see cref="Domain.Platform.RoomStatus.Ended"/> or
    /// <see cref="Domain.Platform.RoomStatus.AwaitingRematch"/>; moves the
    /// room to <see cref="Domain.Platform.RoomStatus.Closed"/> and notifies
    /// the still-present player via <c>OpponentExited</c>. Idempotent on
    /// <c>Closed</c> (no broadcast) so the "Back to lobby" button is safe
    /// to click after the opponent already exited.
    /// </summary>
    public async Task<RoomDto> ExitRoom()
    {
        var session = RequireSession();
        var cmd = new ExitRoomCommand(
            session.RoomCode.Value, session.PlayerId.Value, session.Role);

        var result = await _exitRoom.HandleAsync(cmd, Context.ConnectionAborted);
        if (!result.Succeeded)
        {
            throw new HubException(result.Error!);
        }

        var value = result.Value!;
        if (value.Transitioned)
        {
            await Clients.OthersInGroup(GroupName(session.RoomCode.Value))
                .SendAsync(RoomHubEvents.OpponentExited,
                    new { role = session.Role, room = value.Room },
                    Context.ConnectionAborted);
        }

        return value.Room;
    }

    /// <summary>
    /// SignalR group name for a room — used by both <see cref="RoomHub"/>
    /// and <c>RoomsController</c> (when it pushes <c>OpponentJoined</c>
    /// after an HTTP join) so the name stays a single source of truth.
    /// </summary>
    public static string GroupName(string roomCode) => $"room:{roomCode}";

    private Session RequireSession()
    {
        if (Context.Items[SessionContextKey] is Session session)
        {
            return session;
        }
        throw new HubException(PlatformErrors.SessionUnauthorized);
    }
}
