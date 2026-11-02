using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;

namespace PlayMe.Infrastructure.Scheduling;

/// <summary>
/// Placeholder <see cref="IBotTurnScheduler"/> until the Redis-backed
/// scheduler and its sweeper land (Sprint 12 PR3). Accepts every schedule
/// and does nothing — bot rooms cannot be created through the API yet, so
/// no room ever depends on an entry firing.
/// </summary>
public sealed class NoOpBotTurnScheduler : IBotTurnScheduler
{
    public Task ScheduleAsync(RoomCode code, DateTimeOffset dueAt, CancellationToken ct) =>
        Task.CompletedTask;
}
