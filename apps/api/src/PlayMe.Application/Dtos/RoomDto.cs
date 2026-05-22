using System.Text.Json;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Dtos;

/// <summary>
/// Wire view of a room. Carries everything a client needs to render the
/// room without further round-trips. Returned by create / join / get
/// endpoints and on SignalR state-broadcast events. Does not include any
/// player IDs (CLAUDE.md §5.4 — IDs stay in the signed session cookie).
/// <para>
/// <see cref="GameOptions"/> (Sprint 9 PR1) is the opaque per-room options
/// blob the host chose at room creation — the challenger reads it from the
/// join-info response to render game-specific configuration (e.g. board
/// size) before committing to join. Null for games without options.
/// </para>
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
    ScoreDto Score,
    Role? RematchOffererRole,
    JsonElement? GameOptions = null);
