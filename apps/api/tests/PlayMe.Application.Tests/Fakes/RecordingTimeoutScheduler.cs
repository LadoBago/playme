using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Tests.Fakes;

/// <summary>
/// Records every call so tests can assert that <c>SubmitMoveHandler</c>
/// and <c>RegisterPresenceHandler</c> schedule/cancel timeouts at the
/// expected moments.
/// </summary>
public sealed class RecordingTimeoutScheduler : ITimeoutScheduler
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
