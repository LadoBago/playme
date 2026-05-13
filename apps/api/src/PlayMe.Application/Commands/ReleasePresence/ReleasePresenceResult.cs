using PlayMe.Application.Dtos;

namespace PlayMe.Application.Commands.ReleasePresence;

/// <summary>
/// Result of clearing presence. <see cref="OpponentNotificationDue"/> is
/// true when the Hub should broadcast <c>OpponentDisconnected</c> to the
/// still-present player — only meaningful while the room is
/// <c>InProgress</c> (no-op while <c>WaitingForOpponent</c> per state.md
/// §2.1, and post-match exits flow through Sprint 5's <c>ExitRoom</c>).
/// </summary>
public sealed record ReleasePresenceResult(
    RoomDto Room,
    bool OpponentNotificationDue);
