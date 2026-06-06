using PlayMe.Application.Dtos;

namespace PlayMe.Application.Commands.AdjudicateSetupTimeout;

/// <summary>
/// Adjudication outcome for a fired setup deadline.
/// <paramref name="Expired"/> false means the entry was stale (setup
/// completed, match already ended, room gone) and was dropped. Setup
/// expiry never ends a match — no forfeit path exists; the deadline
/// expires the room without awarding a win.
/// <paramref name="Room"/> is the post-adjudication snapshot when a
/// broadcast is needed.
/// </summary>
public sealed record AdjudicateSetupTimeoutResult(
    RoomDto? Room,
    bool Expired);
