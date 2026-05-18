using PlayMe.Domain.Platform;

namespace PlayMe.Application.Dtos;

/// <summary>
/// Wire view of a room. Carries everything a client needs to render the
/// room without further round-trips. Returned by create / join / get
/// endpoints and on SignalR state-broadcast events. Does not include any
/// player IDs (CLAUDE.md §5.4 — IDs stay in the signed session cookie).
/// </summary>
public sealed record RoomDto(
    RoomCode Code,
    GameId GameId,
    SideSelectionMode SideSelectionMode,
    RoomStatus Status,
    PlayerDto Host,
    PlayerDto? Challenger,
    bool HostConnected,
    bool ChallengerConnected,
    MatchDto? CurrentMatch,
    DateTimeOffset CreatedAt,
    ScoreDto Score);
