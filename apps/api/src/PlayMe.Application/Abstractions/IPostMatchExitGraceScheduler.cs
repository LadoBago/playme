using PlayMe.Domain.Platform;

namespace PlayMe.Application.Abstractions;

/// <summary>
/// Schedule a brief reconnect grace for a player who disconnects while
/// the room is in <see cref="RoomStatus.Ended"/> or
/// <see cref="RoomStatus.AwaitingRematch"/> (docs/state.md §2.4).
/// If the player reconnects before the deadline, the entry is cancelled
/// and the opponent never sees an exit notice; otherwise the sweeper
/// dispatches <c>AdjudicatePostMatchExitGraceHandler</c>, which transitions
/// the room to <see cref="RoomStatus.Closed"/> and broadcasts
/// <c>OpponentExited</c>.
///
/// Distinct from <see cref="IDisconnectGraceScheduler"/> — that one
/// covers in-progress disconnects and ends the match with
/// <c>Outcome.Disconnect</c>; this one covers post-match disconnects and
/// only handles the room exit. Two schedulers, two sorted sets, two
/// sweepers — they share the <see cref="Domain.Platform.RoomCode"/> +
/// <see cref="Role"/> member encoding via
/// <c>PlayMe.Infrastructure.Scheduling.GraceMemberKey</c>.
/// </summary>
public interface IPostMatchExitGraceScheduler
{
    /// <summary>
    /// Schedule (or replace) a post-match exit grace entry for the given
    /// player slot in <paramref name="code"/> at <paramref name="deadline"/>.
    /// </summary>
    Task ScheduleAsync(
        RoomCode code,
        Role role,
        DateTimeOffset deadline,
        CancellationToken ct);

    /// <summary>
    /// Cancel any pending post-match exit grace entry for the given player
    /// slot. Called on reconnect; no-op if no entry exists.
    /// </summary>
    Task CancelAsync(RoomCode code, Role role, CancellationToken ct);
}
