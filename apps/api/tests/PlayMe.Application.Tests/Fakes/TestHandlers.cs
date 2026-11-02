using PlayMe.Application.Abstractions;
using PlayMe.Application.Bots;
using PlayMe.Application.Commands.SubmitMove;
using PlayMe.Application.Commands.SubmitSetup;
using PlayMe.Application.Moves;
using PlayMe.Application.Setup;
using PlayMe.Application.Time;

namespace PlayMe.Application.Tests.Fakes;

/// <summary>
/// Builders for handlers whose constructors grew the shared pipeline
/// tails (<see cref="AcceptedMoveCommitter"/>, <see cref="SetupCommitter"/>)
/// and the <see cref="BotTurnPlanner"/> in Sprint 12. Tests that don't
/// care about those pass only what they assert on; the rest default to
/// recording fakes.
/// </summary>
public static class TestHandlers
{
    public static BotTurnPlanner BotTurns(RecordingBotTurnScheduler? scheduler = null) =>
        new(scheduler ?? new RecordingBotTurnScheduler());

    public static AcceptedMoveCommitter MoveCommitter(
        IRoomRepository rooms,
        ITimeoutScheduler timeouts,
        IDisconnectGraceScheduler? graces = null,
        IAnalyticsClient? analytics = null,
        RecordingBotTurnScheduler? botTurns = null) =>
        new(
            rooms,
            timeouts,
            graces ?? new RecordingGraceScheduler(),
            analytics ?? new RecordingAnalyticsClient(),
            BotTurns(botTurns));

    public static SetupCommitter SetupCommitter(
        IRoomRepository rooms,
        ITimeoutScheduler timeouts,
        ISetupDeadlineScheduler? setupDeadlines = null,
        RecordingBotTurnScheduler? botTurns = null) =>
        new(
            rooms,
            timeouts,
            setupDeadlines ?? new RecordingSetupDeadlineScheduler(),
            BotTurns(botTurns));

    public static SubmitMoveHandler SubmitMove(
        IRoomRepository rooms,
        IGameModuleRegistry games,
        IClock clock,
        ITimeoutScheduler timeouts,
        IDisconnectGraceScheduler? graces = null,
        IRateLimiter? rateLimiter = null,
        IAnalyticsClient? analytics = null,
        RecordingBotTurnScheduler? botTurns = null) =>
        new(
            rooms,
            games,
            clock,
            new ClockService(),
            rateLimiter ?? new RecordingRateLimiter(),
            MoveCommitter(rooms, timeouts, graces, analytics, botTurns));

    public static SubmitSetupHandler SubmitSetup(
        IRoomRepository rooms,
        IGameModuleRegistry games,
        IClock clock,
        ITimeoutScheduler timeouts,
        ISetupDeadlineScheduler? setupDeadlines = null,
        IRateLimiter? rateLimiter = null,
        RecordingBotTurnScheduler? botTurns = null) =>
        new(
            rooms,
            games,
            clock,
            rateLimiter ?? new RecordingRateLimiter(),
            SetupCommitter(rooms, timeouts, setupDeadlines, botTurns));
}
