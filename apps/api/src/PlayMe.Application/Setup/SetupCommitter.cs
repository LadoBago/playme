using PlayMe.Application.Abstractions;
using PlayMe.Application.Bots;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Setup;

/// <summary>
/// The tail of the setup-commit pipeline (Sprint 10 seam C), shared by the
/// human path (<c>SubmitSetupHandler</c>) and the bot path
/// (<c>PlayBotSetupHandler</c>) so the two cannot drift (Sprint 12 seam
/// D). The caller has validated the payload through
/// <see cref="ISetupGame.ValidateSetup"/>; this applies it, records the
/// commit, and — when the module reports setup complete — starts play:
/// clock re-stamped, setup deadline cancelled, first no-move timeout
/// armed, and the bot's first action scheduled if it is the bot's. Must be
/// called inside the room lock.
/// </summary>
public sealed class SetupCommitter
{
    private readonly IRoomRepository _rooms;
    private readonly ITimeoutScheduler _timeouts;
    private readonly ISetupDeadlineScheduler _setupDeadlines;
    private readonly BotTurnPlanner _botTurns;

    public SetupCommitter(
        IRoomRepository rooms,
        ITimeoutScheduler timeouts,
        ISetupDeadlineScheduler setupDeadlines,
        BotTurnPlanner botTurns)
    {
        _rooms = rooms;
        _timeouts = timeouts;
        _setupDeadlines = setupDeadlines;
        _botTurns = botTurns;
    }

    /// <summary>
    /// Apply a validated setup for <paramref name="committerRole"/> and
    /// persist. Returns true when this commit completed the setup phase
    /// and the match is now <see cref="RoomStatus.InProgress"/>.
    /// </summary>
    public async Task<bool> CommitAsync(
        Room room,
        ISetupGame setupGame,
        string committerSide,
        Role committerRole,
        GameMove setup,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(setupGame);
        var match = room.CurrentMatch
            ?? throw new InvalidOperationException("No match to apply a setup to.");

        var newState = setupGame.ApplySetup(match.State, committerSide, setup);
        match.ApplySetup(newState, committerRole);

        var matchStarted = setupGame.IsSetupComplete(match.State);
        if (matchStarted)
        {
            room.CompleteSetup(now);
            await _rooms.SaveAsync(room, ct);
            await _setupDeadlines.CancelAsync(room.Code, ct);
            // The first mover's clock starts now (docs/platform.md §1
            // #12) — schedule the first no-move timeout check.
            await _timeouts.ScheduleAsync(room.Code, match.Clock.ActivePlayerDeadline(), ct);
        }
        else
        {
            await _rooms.SaveAsync(room, ct);
        }

        // Either the bot still owes its setup, or play began and the bot
        // may be the first mover. For a human room this is a no-op.
        await _botTurns.ScheduleIfPendingAsync(room, now, ct);
        return matchStarted;
    }
}
