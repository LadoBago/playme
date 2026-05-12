namespace PlayMe.Application.Queries.GetRoom;

/// <summary>
/// Read a room snapshot by code (CLAUDE.md §2.5 join flow + initial page
/// hydration). No authorization on the read itself — any holder of the
/// opaque code can fetch the snapshot, since the code IS the access control
/// (§5.4). PlayerIds never appear in the response (PlayerDto omits them).
/// </summary>
public sealed record GetRoomQuery(string RoomCode);
