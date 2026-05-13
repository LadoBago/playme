namespace PlayMe.Application.Commands.AdjudicateTimeout;

/// <summary>
/// Sweeper-side dispatch when a <c>playme:timeouts</c> entry expires
/// (state.md §2.2). The sweeper has already acquired the per-room
/// distributed lock; this handler re-reads the room state and decides
/// whether the active player has actually timed out — a move may have
/// landed since the entry was scheduled and invalidated the deadline.
/// </summary>
public sealed record AdjudicateTimeoutCommand(string RoomCode);
