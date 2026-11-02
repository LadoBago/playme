using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Bots;

/// <summary>
/// Schedules the bot's next action after a room state change (Sprint 12
/// seam D). Every handler that can hand the turn to the bot — presence
/// registration starting the match, an accepted human move, a human setup
/// commit, a rematch, and the bot's own retained turn — calls
/// <see cref="ScheduleIfPendingAsync"/> once it has persisted the room.
/// For human-vs-human rooms the call is a cheap no-op, so the handlers
/// stay oblivious to whether a bot is seated.
/// </summary>
public sealed class BotTurnPlanner
{
    private readonly IBotTurnScheduler _botTurns;

    public BotTurnPlanner(IBotTurnScheduler botTurns)
    {
        _botTurns = botTurns;
    }

    /// <summary>
    /// Enroll the bot's next action at <paramref name="now"/> plus
    /// <see cref="PlatformConstants.BotThinkDelay"/> when
    /// <see cref="BotTurnPolicy.PendingAction"/> says the bot owes one.
    /// Returns the action that was scheduled, or <see cref="BotAction.None"/>.
    /// </summary>
    public async Task<BotAction> ScheduleIfPendingAsync(Room room, DateTimeOffset now, CancellationToken ct)
    {
        var action = BotTurnPolicy.PendingAction(room);
        if (action != BotAction.None)
        {
            await _botTurns.ScheduleAsync(room.Code, now + PlatformConstants.BotThinkDelay, ct);
        }
        return action;
    }
}
