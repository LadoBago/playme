using PlayMe.Application.Dtos;

namespace PlayMe.Application.Commands.SubmitMove;

/// <summary>
/// Result of an accepted move. <see cref="MatchEnded"/> is true when the
/// move terminated the match (win, draw, resign, timeout), so the Hub knows
/// to broadcast <c>MatchEnded</c> in addition to <c>MoveAccepted</c>.
/// <see cref="TimedOut"/> distinguishes the case where the caller's clock
/// had already run out at the moment of the submission: no
/// <c>MoveAccepted</c> is broadcast, only <c>MatchEnded</c> with
/// <c>Outcome.Timeout</c>. The terminating <c>Outcome</c> itself, and any
/// per-game "what just happened" hints (the cell that filled, the disc
/// that landed, …), are always reachable via <c>Room.CurrentMatch</c> —
/// the platform doesn't carry per-game move details (CLAUDE.md §7
/// "Platform thinness").
/// </summary>
public sealed record SubmitMoveResult(
    RoomDto Room,
    bool MatchEnded,
    bool TimedOut);
