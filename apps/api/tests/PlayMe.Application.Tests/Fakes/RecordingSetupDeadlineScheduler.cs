using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Tests.Fakes;

/// <summary>
/// Test fake for <see cref="ISetupDeadlineScheduler"/> (Sprint 10 seam C).
/// Records schedule/cancel calls so tests can assert the setup deadline
/// was enrolled at SettingUp entry and cancelled on completion.
/// </summary>
public sealed class RecordingSetupDeadlineScheduler : ISetupDeadlineScheduler
{
    public List<(string RoomCode, DateTimeOffset Deadline)> Scheduled { get; } = new();

    public List<string> Cancelled { get; } = new();

    public Task ScheduleAsync(RoomCode code, DateTimeOffset deadline, CancellationToken ct)
    {
        Scheduled.Add((code.Value, deadline));
        return Task.CompletedTask;
    }

    public Task CancelAsync(RoomCode code, CancellationToken ct)
    {
        Cancelled.Add(code.Value);
        return Task.CompletedTask;
    }
}
