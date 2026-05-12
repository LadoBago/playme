using PlayMe.Domain.Platform;

namespace PlayMe.Infrastructure.Redis;

/// <summary>
/// JSON-friendly persisted shape of <see cref="Match"/>. The per-game state
/// is flattened to a row-major <c>StateCells</c> array with explicit
/// <c>StateRows</c>/<c>StateCols</c> — works for every grid game in the
/// v1 catalog (3×3, 6×6, 9×9, and 7×6 Connect 4) without per-game schemas.
///
/// Clock fields mirror <see cref="MatchClock"/> as millisecond counters
/// (state.md §1: <c>hostClockMs</c>, <c>challengerClockMs</c>,
/// <c>activePlayer</c>, <c>lastTickAt</c>).
/// </summary>
internal sealed record MatchRecord(
    GameId GameId,
    string SideToMove,
    int MoveCount,
    int StateRows,
    int StateCols,
    IReadOnlyList<string?> StateCells,
    long HostClockMs,
    long ChallengerClockMs,
    Role ActivePlayer,
    DateTimeOffset LastTickAt,
    OutcomeRecord? Outcome);
