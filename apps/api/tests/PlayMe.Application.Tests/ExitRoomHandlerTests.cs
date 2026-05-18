using FluentAssertions;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Commands.ExitRoom;
using PlayMe.Application.Errors;
using PlayMe.Application.RateLimiting;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Application.Time;
using PlayMe.Domain.Games.TicTacToe3x3;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Authorization, idempotency, and rate-limit behaviour of the
/// post-match exit pipeline (docs/state.md §2.4). The domain
/// state-machine itself lives in <see cref="RoomExitTests"/>.
/// </summary>
public sealed class ExitRoomHandlerTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(60);

    private static ExitRoomHandler BuildHandler(
        FakeClock clock,
        FakeRoomRepository rooms,
        IRateLimiter? limiter = null) =>
        new(rooms, new SingleGameRegistry(), clock, limiter ?? new RecordingRateLimiter());

    [Fact]
    public async Task Exit_from_Ended_transitions_and_signals_Transitioned_true()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        rooms.Seed(EndedRoom(clock));
        var handler = BuildHandler(clock, rooms);

        var result = await handler.HandleAsync(
            new ExitRoomCommand(
                RoomFactory.RoomCodeValue,
                RoomFactory.HostPlayerId,
                Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Transitioned.Should().BeTrue();

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.Status.Should().Be(RoomStatus.Closed);
    }

    [Fact]
    public async Task Second_Exit_is_idempotent_Transitioned_false_no_error()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        rooms.Seed(EndedRoom(clock));
        var handler = BuildHandler(clock, rooms);

        // First call closes.
        var first = await handler.HandleAsync(
            new ExitRoomCommand(
                RoomFactory.RoomCodeValue,
                RoomFactory.HostPlayerId,
                Role.Host),
            CancellationToken.None);
        first.Value!.Transitioned.Should().BeTrue();

        // Opponent's "Back to lobby" click after their tab already saw
        // OpponentExited — must succeed with Transitioned=false, no
        // re-broadcast triggered by the Hub layer.
        var second = await handler.HandleAsync(
            new ExitRoomCommand(
                RoomFactory.RoomCodeValue,
                RoomFactory.ChallengerPlayerId,
                Role.Challenger),
            CancellationToken.None);

        second.Succeeded.Should().BeTrue();
        second.Value!.Transitioned.Should().BeFalse();
    }

    [Fact]
    public async Task Exit_from_InProgress_returns_ExitNotAllowed()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        rooms.Seed(RoomFactory.InProgress(clock.UtcNow, Budget));
        var handler = BuildHandler(clock, rooms);

        var result = await handler.HandleAsync(
            new ExitRoomCommand(
                RoomFactory.RoomCodeValue,
                RoomFactory.HostPlayerId,
                Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(PlatformErrors.ExitNotAllowed);

        // Room is unchanged.
        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.Status.Should().Be(RoomStatus.InProgress);
    }

    [Fact]
    public async Task Exit_with_invalid_session_returns_Unauthorized()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        rooms.Seed(EndedRoom(clock));
        var handler = BuildHandler(clock, rooms);

        var result = await handler.HandleAsync(
            new ExitRoomCommand(
                RoomFactory.RoomCodeValue,
                CallerPlayerId: "not-the-real-player",
                Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(PlatformErrors.SessionUnauthorized);
    }

    [Fact]
    public async Task Exit_short_circuits_on_rate_limit()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var limiter = new RecordingRateLimiter { AllowNext = false };
        rooms.Seed(EndedRoom(clock));
        var handler = BuildHandler(clock, rooms, limiter);

        var result = await handler.HandleAsync(
            new ExitRoomCommand(
                RoomFactory.RoomCodeValue,
                RoomFactory.HostPlayerId,
                Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(PlatformErrors.RateExceeded);

        limiter.Calls.Should().ContainSingle()
            .Which.Should().Be((SessionRateLimitPolicies.ExitRoom, RoomFactory.HostPlayerId));

        // Room unchanged: lock never acquired.
        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.Status.Should().Be(RoomStatus.Ended);
    }

    private static Room EndedRoom(FakeClock clock)
    {
        var room = RoomFactory.InProgress(clock.UtcNow, Budget);
        room.CurrentMatch!.Resign(TicTacToeSides.X);
        room.EndCurrentMatch();
        return room;
    }
}
