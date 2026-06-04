using PlayMe.Domain.Platform;

namespace PlayMe.Application.Abstractions;

/// <summary>
/// Schedules the one setup-phase deadline per room (Sprint 10 seam C;
/// docs/state.md §2.2 sweeper pattern). Enrolled when a room enters
/// <see cref="RoomStatus.SettingUp"/> at <c>now + ISetupGame.SetupBudget</c>;
/// cancelled when setup completes or the match ends during setup. When it
/// fires, <c>AdjudicateSetupTimeoutHandler</c> forfeits the uncommitted
/// side — or expires the room if neither side committed. Implemented in
/// Infrastructure by the <c>playme:setup_deadlines</c> sorted set.
/// </summary>
public interface ISetupDeadlineScheduler
{
    Task ScheduleAsync(RoomCode code, DateTimeOffset deadline, CancellationToken ct);

    Task CancelAsync(RoomCode code, CancellationToken ct);
}
