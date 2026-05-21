using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Tests.Fakes;

/// <summary>
/// Records every call so tests can assert that the release-/register-
/// presence handlers manage the post-match exit grace window correctly
/// across disconnect/reconnect (docs/state.md §2.4).
/// </summary>
public sealed class RecordingPostMatchExitGraceScheduler : IPostMatchExitGraceScheduler
{
    public List<(string RoomCode, Role Role, DateTimeOffset Deadline)> Scheduled { get; } = new();
    public List<(string RoomCode, Role Role)> Cancelled { get; } = new();

    public Task ScheduleAsync(
        RoomCode code,
        Role role,
        DateTimeOffset deadline,
        CancellationToken ct)
    {
        Scheduled.Add((code.Value, role, deadline));
        return Task.CompletedTask;
    }

    public Task CancelAsync(RoomCode code, Role role, CancellationToken ct)
    {
        Cancelled.Add((code.Value, role));
        return Task.CompletedTask;
    }
}
