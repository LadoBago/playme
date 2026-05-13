using PlayMe.Domain.Platform;

namespace PlayMe.Application.Dtos;

/// <summary>
/// Wire view of the current match. The per-game board state is shipped as an
/// opaque <see cref="State"/> string produced by <see cref="IGameModule.Serialize"/>
/// — the platform never enumerates board shape (CLAUDE.md §7 "Platform
/// thinness"). The per-game web renderer parses <see cref="State"/> as it
/// sees fit.
/// </summary>
/// <param name="GameId">Which game this match is playing.</param>
/// <param name="SideToMove">Side whose turn it is. Still set after the
/// match ends so the client can show "X's turn" → final state cleanly.</param>
/// <param name="MoveCount">Total accepted moves in this match.</param>
/// <param name="State">Opaque per-game state blob (JSON produced by the
/// game module). The platform passes this through unchanged.</param>
/// <param name="Clock">Server-authoritative clock snapshot — see
/// <see cref="ClockSnapshotDto"/>. Sent on every event that mutates the
/// match (start, accepted move, timeout, match end) so clients can
/// re-sync without a separate request.</param>
/// <param name="Outcome">Non-null once the match terminates.</param>
public sealed record MatchDto(
    GameId GameId,
    string SideToMove,
    int MoveCount,
    string State,
    ClockSnapshotDto Clock,
    OutcomeDto? Outcome);
