using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Tests.Fakes;

/// <summary>
/// Records every bot-turn schedule so tests can assert that the platform
/// enrolls the bot exactly when the turn (or the setup) becomes its.
/// </summary>
public sealed class RecordingBotTurnScheduler : IBotTurnScheduler
{
    public List<(string RoomCode, DateTimeOffset DueAt)> Scheduled { get; } = new();

    public Task ScheduleAsync(RoomCode code, DateTimeOffset dueAt, CancellationToken ct)
    {
        Scheduled.Add((code.Value, dueAt));
        return Task.CompletedTask;
    }
}
