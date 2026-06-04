using PlayMe.Application.Dtos;

namespace PlayMe.Application.Commands.AdjudicateSetupTimeout;

/// <summary>
/// Adjudication outcome for a fired setup deadline. Exactly one of
/// <paramref name="MatchEnded"/> / <paramref name="Expired"/> is true on
/// action; both false means the entry was stale (setup completed, match
/// already ended, room gone) and was dropped. <paramref name="Room"/> is
/// the post-adjudication snapshot when a broadcast is needed.
/// </summary>
public sealed record AdjudicateSetupTimeoutResult(
    RoomDto? Room,
    bool MatchEnded,
    bool Expired);
