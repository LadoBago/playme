using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Tests.Fakes;

/// <summary>
/// Records every call so tests can assert that
/// <c>CreateRoomHandler</c> enrolls and <c>RegisterPresenceHandler</c>
/// cancels at the expected moments.
/// </summary>
public sealed class RecordingRoomExpiryScheduler : IRoomExpiryScheduler
{
    public List<(string RoomCode, string GameId, DateTimeOffset Deadline)> Scheduled { get; } = new();
    public List<(string RoomCode, string GameId)> Cancelled { get; } = new();

    public Task ScheduleAsync(
        RoomCode code, GameId gameId, DateTimeOffset deadline, CancellationToken ct)
    {
        Scheduled.Add((code.Value, gameId.Value, deadline));
        return Task.CompletedTask;
    }

    public Task CancelAsync(RoomCode code, GameId gameId, CancellationToken ct)
    {
        Cancelled.Add((code.Value, gameId.Value));
        return Task.CompletedTask;
    }
}
