using PlayMe.Domain.Platform;

namespace PlayMe.Api.Security;

/// <summary>
/// Server-decoded view of a signed session cookie (CLAUDE.md §5.4).
/// Carries the three values that authorize every Hub method and controller
/// action: which room the caller is in, who they are inside that room, and
/// whether they are the host or the challenger.
/// </summary>
public sealed record Session(RoomCode RoomCode, PlayerId PlayerId, Role Role);
