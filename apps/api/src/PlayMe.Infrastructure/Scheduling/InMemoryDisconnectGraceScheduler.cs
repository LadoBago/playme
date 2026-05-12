using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;

namespace PlayMe.Infrastructure.Scheduling;

/// <summary>
/// PR #1 placeholder — same caveats as <see cref="InMemoryTimeoutScheduler"/>.
/// PR #2 replaces this with a Redis-backed sorted set + sweeper.
/// </summary>
public sealed partial class InMemoryDisconnectGraceScheduler : IDisconnectGraceScheduler
{
    private readonly ConcurrentDictionary<(string Code, Role Role), DateTimeOffset> _entries = new();
    private readonly ILogger<InMemoryDisconnectGraceScheduler> _log;

    public InMemoryDisconnectGraceScheduler(ILogger<InMemoryDisconnectGraceScheduler> log)
    {
        _log = log;
    }

    public Task ScheduleAsync(
        RoomCode code,
        Role role,
        DateTimeOffset deadline,
        CancellationToken ct)
    {
        _entries[(code.Value, role)] = deadline;
        LogScheduled(_log, code.Value, role, deadline);
        return Task.CompletedTask;
    }

    public Task CancelAsync(RoomCode code, Role role, CancellationToken ct)
    {
        _entries.TryRemove((code.Value, role), out _);
        return Task.CompletedTask;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "PR1 stub: scheduled grace for {RoomCode}/{Role} at {Deadline}")]
    private static partial void LogScheduled(
        ILogger logger,
        string roomCode,
        Role role,
        DateTimeOffset deadline);
}
