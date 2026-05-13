using PlayMe.Application.Dtos;

namespace PlayMe.Application.Commands.AdjudicateTimeout;

/// <summary>
/// Result of an adjudication pass. <see cref="TimedOut"/> is true only when
/// this call actually transitioned the match to <c>Ended</c> with
/// <c>Outcome.Timeout</c>. False in all "stale entry" cases (a move
/// landed, the room is no longer in progress, etc.) — those are no-ops.
///
/// <see cref="Room"/> is null when the room couldn't be loaded (already
/// expired). The Hub broadcasts a <c>MatchEnded</c> only when
/// <see cref="TimedOut"/> is true.
/// </summary>
public sealed record AdjudicateTimeoutResult(
    RoomDto? Room,
    bool TimedOut);
