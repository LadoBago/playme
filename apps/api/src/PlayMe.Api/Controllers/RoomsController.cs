using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using PlayMe.Api.Hubs;
using PlayMe.Api.Http;
using PlayMe.Api.RateLimiting;
using PlayMe.Api.Security;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Commands.CreateRoom;
using PlayMe.Application.Commands.JoinRoom;
using PlayMe.Application.Dtos;
using PlayMe.Application.Mapping;
using PlayMe.Application.Queries.GetRoom;
using PlayMe.Domain.Platform;

namespace PlayMe.Api.Controllers;

/// <summary>
/// HTTP surface for room lifecycle actions (CLAUDE.md §2.5). All three
/// endpoints are thin: parse request → call Application handler →
/// translate <see cref="Application.AppResult{T}"/> to a response. No
/// business logic.
/// </summary>
[ApiController]
[Route("api/rooms")]
public sealed class RoomsController : ControllerBase
{
    private readonly CreateRoomHandler _createRoom;
    private readonly JoinRoomHandler _joinRoom;
    private readonly GetRoomHandler _getRoom;
    private readonly SessionCookieWriter _cookieWriter;
    private readonly SessionCookieReader _cookieReader;
    private readonly IHubContext<RoomHub> _hubContext;
    private readonly IGameModuleRegistry _games;

    public RoomsController(
        CreateRoomHandler createRoom,
        JoinRoomHandler joinRoom,
        GetRoomHandler getRoom,
        SessionCookieWriter cookieWriter,
        SessionCookieReader cookieReader,
        IHubContext<RoomHub> hubContext,
        IGameModuleRegistry games)
    {
        _createRoom = createRoom;
        _joinRoom = joinRoom;
        _getRoom = getRoom;
        _cookieWriter = cookieWriter;
        _cookieReader = cookieReader;
        _hubContext = hubContext;
        _games = games;
    }

    /// <summary>
    /// Create a new room and issue the host's session cookie. Returns the
    /// initial <see cref="RoomDto"/>; the client navigates to <c>/r/{code}</c>
    /// to open the SignalR connection.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitingPolicies.RoomsCreate)]
    public async Task<ActionResult<RoomDto>> CreateRoom(
        [FromBody] CreateRoomCommand command, CancellationToken ct)
    {
        var result = await _createRoom.HandleAsync(command, ct);
        if (!result.Succeeded)
        {
            return AppResultActionExtensions.ToProblem(result.Error!, result.Detail);
        }

        var value = result.Value!;
        _cookieWriter.Write(Response,
            new Session(value.Room.Code, value.HostPlayerId, Role.Host));

        return CreatedAtAction(
            actionName: nameof(GetRoom),
            routeValues: new { code = value.Room.Code.Value },
            value: value.Room);
    }

    /// <summary>
    /// Atomic challenger registration (CLAUDE.md §2.5 join contract).
    /// On success, mints the challenger's session cookie and broadcasts
    /// <c>OpponentJoined</c> to anyone already in the room's SignalR group
    /// (typically the host).
    /// </summary>
    [HttpPost("{code}/join")]
    [EnableRateLimiting(RateLimitingPolicies.RoomsJoin)]
    public async Task<ActionResult<RoomDto>> JoinRoom(
        [FromRoute] string code,
        [FromBody] JoinRoomRequestBody body,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var command = new JoinRoomCommand(
            RoomCode: code,
            DisplayName: body.DisplayName,
            Side: body.Side);

        var result = await _joinRoom.HandleAsync(command, ct);
        if (!result.Succeeded)
        {
            return AppResultActionExtensions.ToProblem(result.Error!, result.Detail);
        }

        var value = result.Value!;
        _cookieWriter.Write(Response,
            new Session(value.Room.Code, value.ChallengerPlayerId, Role.Challenger));

        // Notify the host's already-open SignalR connection (if any) that
        // the seat is now filled. The match doesn't start until both sides
        // are *also* connected — that transition fires from RoomHub.JoinRoom
        // (RegisterPresence) per §2.9. No per-role projection needed here:
        // a just-joined room is in WaitingForOpponent with no match, so
        // there is no game state to hide (RoomViewProjector would no-op).
        await _hubContext.Clients
            .Group(RoomHub.GroupName(value.Room.Code.Value))
            .SendAsync(RoomHubEvents.OpponentJoined,
                new { room = value.Room },
                ct);

        return Ok(RoomViewProjector.ForViewer(value.Room, Role.Challenger, _games));
    }

    /// <summary>
    /// Fetch a room snapshot by its opaque code. Powers the pre-session
    /// invite-preview landing on <c>/r/{code}</c>.
    /// </summary>
    /// <remarks>
    /// Intentionally anonymous: the invite-link flow shows host/game/rules
    /// before a visitor decides to join (the join request is where their
    /// session cookie is minted). Access control here is by knowledge of
    /// the unguessable 128-bit room code (CSPRNG; see
    /// <c>RoomCodeGenerator</c>), not by session cookie — a deliberate
    /// carve-out from <c>docs/security.md §4</c>'s "auth on every action"
    /// rule. The per-IP rate limit
    /// (<see cref="RateLimitingPolicies.RoomsGet"/>) caps abuse if a code
    /// leaks; entropy makes enumeration intractable.
    /// <para>
    /// Hidden-state games (Sprint 10 seam A): the snapshot is projected
    /// for the caller's role when a valid session cookie for this room is
    /// presented, and to the module's public view otherwise. Without this,
    /// a player could fetch the full state — including the opponent's
    /// hidden information — by hitting this endpoint with the room code
    /// they already know.
    /// </para>
    /// </remarks>
    [HttpGet("{code}")]
    [EnableRateLimiting(RateLimitingPolicies.RoomsGet)]
    public async Task<ActionResult<RoomDto>> GetRoom(
        [FromRoute] string code, CancellationToken ct)
    {
        var result = await _getRoom.HandleAsync(new GetRoomQuery(code), ct);
        if (!result.Succeeded)
        {
            return AppResultActionExtensions.ToProblem(result.Error!, result.Detail);
        }

        var room = result.Value!;
        var session = _cookieReader.Read(Request);
        Role? viewer = session is not null
            && string.Equals(session.RoomCode.Value, room.Code.Value, StringComparison.Ordinal)
            ? session.Role
            : null;

        return Ok(RoomViewProjector.ForViewer(room, viewer, _games));
    }

    /// <summary>
    /// Body for <see cref="JoinRoom"/>. The route carries the room code, so
    /// it's not a body field — keeps the URL shape consistent with REST norms.
    /// </summary>
    public sealed record JoinRoomRequestBody(string DisplayName, string? Side);
}
