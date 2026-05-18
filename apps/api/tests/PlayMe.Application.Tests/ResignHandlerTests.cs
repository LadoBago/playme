using FluentAssertions;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Commands.Resign;
using PlayMe.Application.Errors;
using PlayMe.Application.RateLimiting;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Application.Time;
using PlayMe.Domain.Games.TicTacToe3x3;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Behaviour of the resignation pipeline (docs/platform-and-games.md §1 #8).
/// The handler mirrors the move pipeline's authorization + lock + stale-clock
/// conversion structure, minus per-game module lookup — resign is platform-level.
/// </summary>
public sealed class ResignHandlerTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(60);

    private static ResignHandler BuildHandler(
        FakeClock clock,
        FakeRoomRepository rooms,
        RecordingTimeoutScheduler timeouts,
        IRateLimiter? rateLimiter = null) =>
        new(
            rooms,
            new SingleGameRegistry(),
            clock,
            new ClockService(),
            timeouts,
            rateLimiter ?? new RecordingRateLimiter());

    [Fact]
    public async Task Host_resign_ends_match_with_Resign_outcome_and_cancels_timeout()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        rooms.Seed(RoomFactory.InProgress(clock.UtcNow, Budget));
        var handler = BuildHandler(clock, rooms, timeouts);

        var result = await handler.HandleAsync(
            new ResignCommand(
                RoomFactory.RoomCodeValue,
                RoomFactory.HostPlayerId,
                Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.TimedOut.Should().BeFalse();

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.Status.Should().Be(RoomStatus.Ended);
        saved.CurrentMatch!.Outcome.Should().BeOfType<Resign>()
            .Which.ResigningSide.Should().Be(TicTacToeSides.X);

        timeouts.Cancelled.Should().Contain(RoomFactory.RoomCodeValue);
    }

    [Fact]
    public async Task Challenger_can_resign_outside_their_turn()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        rooms.Seed(RoomFactory.InProgress(clock.UtcNow, Budget));
        var handler = BuildHandler(clock, rooms, timeouts);

        // Active player at match start is Host (plays X) — challenger
        // resigning on the opposite turn must still succeed.
        var result = await handler.HandleAsync(
            new ResignCommand(
                RoomFactory.RoomCodeValue,
                RoomFactory.ChallengerPlayerId,
                Role.Challenger),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.CurrentMatch!.Outcome.Should().BeOfType<Resign>()
            .Which.ResigningSide.Should().Be(TicTacToeSides.O);
    }

    [Fact]
    public async Task Stale_clock_resign_becomes_Timeout_for_active_player()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        rooms.Seed(RoomFactory.InProgress(clock.UtcNow, Budget));
        var handler = BuildHandler(clock, rooms, timeouts);

        // Host's (X, the active player) clock runs out. Whether host
        // or challenger calls resign, the disposition is Timeout(X).
        clock.Advance(Budget + TimeSpan.FromSeconds(1));

        var result = await handler.HandleAsync(
            new ResignCommand(
                RoomFactory.RoomCodeValue,
                RoomFactory.ChallengerPlayerId,
                Role.Challenger),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.TimedOut.Should().BeTrue();

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.Status.Should().Be(RoomStatus.Ended);
        saved.CurrentMatch!.Outcome.Should().BeOfType<Domain.Platform.Timeout>()
            .Which.TimedOutSide.Should().Be(TicTacToeSides.X);
    }

    [Fact]
    public async Task Resign_against_already_ended_match_returns_MatchNotInProgress()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        rooms.Seed(RoomFactory.InProgress(clock.UtcNow, Budget));
        var handler = BuildHandler(clock, rooms, timeouts);

        // First resign closes the match.
        var first = await handler.HandleAsync(
            new ResignCommand(
                RoomFactory.RoomCodeValue,
                RoomFactory.HostPlayerId,
                Role.Host),
            CancellationToken.None);
        first.Succeeded.Should().BeTrue();

        // Second resign — say, the challenger double-clicks too — returns
        // the "match already ended" key. No exception, no re-broadcast.
        var second = await handler.HandleAsync(
            new ResignCommand(
                RoomFactory.RoomCodeValue,
                RoomFactory.ChallengerPlayerId,
                Role.Challenger),
            CancellationToken.None);

        second.Succeeded.Should().BeFalse();
        second.Error.Should().Be(PlatformErrors.MoveMatchNotInProgress);
    }

    [Fact]
    public async Task Resign_with_invalid_session_returns_Unauthorized()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        rooms.Seed(RoomFactory.InProgress(clock.UtcNow, Budget));
        var handler = BuildHandler(clock, rooms, timeouts);

        var result = await handler.HandleAsync(
            new ResignCommand(
                RoomFactory.RoomCodeValue,
                CallerPlayerId: "not-the-real-player",
                Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(PlatformErrors.SessionUnauthorized);

        // Room is unchanged.
        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.Status.Should().Be(RoomStatus.InProgress);
    }

    [Fact]
    public async Task Resign_short_circuits_on_rate_limit()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        var limiter = new RecordingRateLimiter { AllowNext = false };
        rooms.Seed(RoomFactory.InProgress(clock.UtcNow, Budget));
        var handler = BuildHandler(clock, rooms, timeouts, limiter);

        var result = await handler.HandleAsync(
            new ResignCommand(
                RoomFactory.RoomCodeValue,
                RoomFactory.HostPlayerId,
                Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(PlatformErrors.RateExceeded);

        limiter.Calls.Should().ContainSingle()
            .Which.Should().Be((SessionRateLimitPolicies.Resign, RoomFactory.HostPlayerId));

        // Room unchanged: lock was never acquired.
        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.Status.Should().Be(RoomStatus.InProgress);
    }
}
