using PlayMe.Domain.Platform;

namespace PlayMe.Infrastructure.Redis;

/// <summary>
/// JSON-friendly persisted shape of <see cref="Match"/>. The per-game state
/// is stored as an opaque <see cref="State"/> string produced by
/// <see cref="IGameModule.Serialize"/> — the platform persistence layer
/// never inspects the shape (CLAUDE.md §7 "Platform thinness").
///
/// Clock fields mirror <see cref="MatchClock"/> as millisecond counters
/// (state.md §1: <c>hostClockMs</c>, <c>challengerClockMs</c>,
/// <c>activePlayer</c>, <c>lastTickAt</c>).
/// </summary>
internal sealed record MatchRecord(
    GameId GameId,
    string SideToMove,
    int MoveCount,
    string State,
    long HostClockMs,
    long ChallengerClockMs,
    Role ActivePlayer,
    DateTimeOffset LastTickAt,
    OutcomeRecord? Outcome,
    // Setup-phase commit flags (Sprint 10 seam C). Default false so blobs
    // persisted before the seam deserialize unchanged.
    bool HostSetupCommitted = false,
    bool ChallengerSetupCommitted = false);
