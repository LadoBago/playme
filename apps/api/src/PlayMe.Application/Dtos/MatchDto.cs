using PlayMe.Domain.Platform;

namespace PlayMe.Application.Dtos;

/// <summary>
/// Wire view of the current match. The board is sent as a flat row-major
/// <see cref="Cells"/> array with explicit <see cref="Rows"/>/<see cref="Cols"/>
/// — the same shape works for every grid game in the v1 catalog (3×3, 6×6,
/// 9×9, and 7×6 Connect 4), so the client board renderer doesn't hard-code
/// a size.
/// </summary>
/// <param name="GameId">Which game this match is playing.</param>
/// <param name="SideToMove">Side whose turn it is. Still set after the
/// match ends so the client can show "X's turn" → final state cleanly.</param>
/// <param name="MoveCount">Total accepted moves in this match.</param>
/// <param name="Cells">Row-major board: side string ("x", "o", ...) or null
/// for empty.</param>
/// <param name="Clock">Server-authoritative clock snapshot — see
/// <see cref="ClockSnapshotDto"/>. Sent on every event that mutates the
/// match (start, accepted move, timeout, match end) so clients can
/// re-sync without a separate request.</param>
/// <param name="Outcome">Non-null once the match terminates.</param>
public sealed record MatchDto(
    GameId GameId,
    string SideToMove,
    int MoveCount,
    int Rows,
    int Cols,
    IReadOnlyList<string?> Cells,
    ClockSnapshotDto Clock,
    OutcomeDto? Outcome);
