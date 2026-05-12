using PlayMe.Domain.Platform;

namespace PlayMe.Application.Dtos;

/// <summary>
/// Caller-scoped view of a room: the room state plus the caller's
/// <see cref="Role"/>. Returned from <c>RoomHub.JoinRoom</c> so the web
/// client doesn't have to guess which seat the (HttpOnly) session cookie
/// authorizes it for. The cookie is encrypted server-side, so the client
/// otherwise has no way to know whether it is the host or the challenger.
/// </summary>
public sealed record RoomSessionDto(Role Role, RoomDto Room);
