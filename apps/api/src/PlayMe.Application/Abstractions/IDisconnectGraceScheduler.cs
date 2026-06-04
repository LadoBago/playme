using PlayMe.Domain.Platform;

namespace PlayMe.Application.Abstractions;

/// <summary>
/// Schedule a future grace-window check for a disconnected player. The
/// 30 s grace from platform.md §1 #7: if the player hasn't
/// reconnected by the deadline, the sweeper dispatches the
/// <c>AdjudicateDisconnectGraceHandler</c>.
///
/// Sprint 2 schedules and cancels entries but the consumer is a no-op
/// stub — Sprint 5 hangs <c>OpponentAbandoned</c> / <c>ClaimVictory</c>
/// off this same path so the wiring stays put.
///
/// Implementation: <c>RedisDisconnectGraceScheduler</c> in Infrastructure
/// (PR #2). Keyed by <c>(roomCode, role)</c> so each player has at most
/// one outstanding entry.
/// </summary>
public interface IDisconnectGraceScheduler
{
    /// <summary>
    /// Schedule (or replace) a grace-window entry for the given player
    /// slot in <paramref name="code"/> at <paramref name="deadline"/>.
    /// </summary>
    Task ScheduleAsync(
        RoomCode code,
        Role role,
        DateTimeOffset deadline,
        CancellationToken ct);

    /// <summary>
    /// Cancel any pending grace-window entry for the given player slot.
    /// Called on reconnect; no-op if no entry exists.
    /// </summary>
    Task CancelAsync(RoomCode code, Role role, CancellationToken ct);
}
