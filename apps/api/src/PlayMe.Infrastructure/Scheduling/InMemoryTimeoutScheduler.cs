using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;

namespace PlayMe.Infrastructure.Scheduling;

/// <summary>
/// PR #1 placeholder — keeps a per-room deadline in an in-process
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> so DI is satisfied and
/// the handlers can call <see cref="ITimeoutScheduler"/>. <strong>Not
/// production-correct</strong>: per-process state breaks across the
/// horizontally-scaled API (state.md §1), and there is no sweeper. PR #2
/// replaces this with <c>RedisTimeoutScheduler</c> backed by
/// <c>playme:timeouts</c> and a <c>BackgroundService</c> sweeper.
/// </summary>
public sealed partial class InMemoryTimeoutScheduler : ITimeoutScheduler
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _entries = new();
    private readonly ILogger<InMemoryTimeoutScheduler> _log;

    public InMemoryTimeoutScheduler(ILogger<InMemoryTimeoutScheduler> log)
    {
        _log = log;
    }

    public Task ScheduleAsync(RoomCode code, DateTimeOffset deadline, CancellationToken ct)
    {
        _entries[code.Value] = deadline;
        LogScheduled(_log, code.Value, deadline);
        return Task.CompletedTask;
    }

    public Task CancelAsync(RoomCode code, CancellationToken ct)
    {
        _entries.TryRemove(code.Value, out _);
        return Task.CompletedTask;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "PR1 stub: scheduled timeout for {RoomCode} at {Deadline}")]
    private static partial void LogScheduled(ILogger logger, string roomCode, DateTimeOffset deadline);
}
