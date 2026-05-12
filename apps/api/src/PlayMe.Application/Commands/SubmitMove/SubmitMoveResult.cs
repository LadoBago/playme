using PlayMe.Application.Dtos;

namespace PlayMe.Application.Commands.SubmitMove;

/// <summary>
/// Result of an accepted move. <see cref="MatchEnded"/> is true when the
/// move terminated the match (win, draw, or — Sprint 2 — timeout), so the
/// Hub knows to broadcast <c>MatchEnded</c> in addition to
/// <c>MoveAccepted</c>. <see cref="TimedOut"/> distinguishes the case
/// where the caller's clock had already run out at the moment of the
/// submission: no <c>MoveAccepted</c> is broadcast, only <c>MatchEnded</c>
/// with <c>Outcome.Timeout</c>. The terminating <c>Outcome</c> itself is
/// always reachable via <c>Room.CurrentMatch.Outcome</c> on the DTO.
/// </summary>
public sealed record SubmitMoveResult(
    RoomDto Room,
    bool MatchEnded,
    bool TimedOut,
    int? AcceptedCell,
    string? ByMoveSide);
