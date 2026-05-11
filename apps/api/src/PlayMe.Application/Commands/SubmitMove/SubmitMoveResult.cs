using PlayMe.Application.Dtos;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.SubmitMove;

/// <summary>
/// Result of an accepted move. <see cref="MatchEnded"/> is true when the
/// move terminated the match (win or draw in Sprint 1), so the Hub knows to
/// broadcast <c>MatchEnded</c> in addition to <c>MoveAccepted</c>. The
/// terminating <see cref="Outcome"/> is also reachable via
/// <c>Room.CurrentMatch.Outcome</c> on the DTO.
/// </summary>
public sealed record SubmitMoveResult(
    RoomDto Room,
    bool MatchEnded,
    int AcceptedCell,
    string ByMoveSide);
