using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using PlayMe.Api.Hubs;
using PlayMe.Api.Http;
using PlayMe.Api.Security;
using PlayMe.Application.Commands.CreateRoom;
using PlayMe.Application.Commands.JoinRoom;
using PlayMe.Application.Dtos;
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
    private readonly IHubContext<RoomHub> _hubContext;

    public RoomsController(
        CreateRoomHandler createRoom,
        JoinRoomHandler joinRoom,
        GetRoomHandler getRoom,
        SessionCookieWriter cookieWriter,
        IHubContext<RoomHub> hubContext)
    {
        _createRoom = createRoom;
        _joinRoom = joinRoom;
        _getRoom = getRoom;
        _cookieWriter = cookieWriter;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Create a new room and issue the host's session cookie. Returns the
    /// initial <see cref="RoomDto"/>; the client navigates to <c>/r/{code}</c>
    /// to open the SignalR connection.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<RoomDto>> CreateRoom(
        [FromBody] CreateRoomCommand command, CancellationToken ct)
    {
        var result = await _createRoom.HandleAsync(command, ct);
        if (!result.Succeeded)
        {
            return AppResultActionExtensions.ToProblem(result.Error!.Value, result.Detail);
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
            return AppResultActionExtensions.ToProblem(result.Error!.Value, result.Detail);
        }

        var value = result.Value!;
        _cookieWriter.Write(Response,
            new Session(value.Room.Code, value.ChallengerPlayerId, Role.Challenger));

        // Notify the host's already-open SignalR connection (if any) that
        // the seat is now filled. The match doesn't start until both sides
        // are *also* connected — that transition fires from RoomHub.JoinRoom
        // (RegisterPresence) per §2.9.
        await _hubContext.Clients
            .Group(RoomHub.GroupName(value.Room.Code.Value))
            .SendAsync(RoomHubEvents.OpponentJoined,
                new { room = value.Room },
                ct);

        return Ok(value.Room);
    }

    [HttpGet("{code}")]
    public async Task<ActionResult<RoomDto>> GetRoom(
        [FromRoute] string code, CancellationToken ct)
    {
        var result = await _getRoom.HandleAsync(new GetRoomQuery(code), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Body for <see cref="JoinRoom"/>. The route carries the room code, so
    /// it's not a body field — keeps the URL shape consistent with REST norms.
    /// </summary>
    public sealed record JoinRoomRequestBody(string DisplayName, string? Side);
}
