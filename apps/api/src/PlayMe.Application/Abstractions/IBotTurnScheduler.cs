using PlayMe.Domain.Platform;

namespace PlayMe.Application.Abstractions;

/// <summary>
/// Schedule the bot's next action for a bot room (Sprint 12 seam D;
/// docs/state.md §2.1). Same shape as <see cref="ITimeoutScheduler"/>:
/// one entry per room, overwritten on re-schedule, picked up by a sweeper
/// in Infrastructure that dispatches <c>PlayBotMoveHandler</c> or
/// <c>PlayBotSetupHandler</c> according to the room's state at fire time.
/// Entries are validated when they fire, never cancelled: a stale entry
/// (match ended, human resigned, room closed) is a cheap no-op in the
/// handler, so the platform does not track cancellation for bot turns.
///
/// Implementation: <c>RedisBotTurnScheduler</c> backing the
/// <c>playme:bot_turns</c> sorted set (Sprint 12 PR3); a no-op stands in
/// until then.
/// </summary>
public interface IBotTurnScheduler
{
    /// <summary>
    /// Schedule (or replace) the single bot-action entry for
    /// <paramref name="code"/> to fire at <paramref name="dueAt"/>.
    /// </summary>
    Task ScheduleAsync(RoomCode code, DateTimeOffset dueAt, CancellationToken ct);
}
