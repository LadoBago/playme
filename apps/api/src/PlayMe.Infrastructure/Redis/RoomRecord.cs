using PlayMe.Domain.Platform;

namespace PlayMe.Infrastructure.Redis;

/// <summary>
/// JSON-friendly persisted shape of <see cref="Room"/>. The full aggregate
/// is stored as one JSON blob per CLAUDE.md §2.8 (no decomposition into
/// Redis hashes / multiple keys).
/// </summary>
internal sealed record RoomRecord(
    RoomCode Code,
    GameId GameId,
    SideSelectionMode SideSelectionMode,
    DateTimeOffset CreatedAt,
    PlayerRecord Host,
    PlayerRecord? Challenger,
    RoomStatus Status,
    MatchRecord? CurrentMatch,
    bool HostConnected,
    bool ChallengerConnected);
