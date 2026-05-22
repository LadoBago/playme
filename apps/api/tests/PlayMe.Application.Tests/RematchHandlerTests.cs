using FluentAssertions;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Commands.AcceptRematch;
using PlayMe.Application.Commands.OfferRematch;
using PlayMe.Application.Commands.RejectRematch;
using PlayMe.Application.Errors;
using PlayMe.Application.RateLimiting;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Application.Time;
using PlayMe.Domain.Games.TicTacToe;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Application-layer behaviour of the rematch handshake: authorization,
/// state validation, timeout scheduling on accept, rate-limit short-circuit.
/// Pure domain transitions live in <see cref="RoomRematchTests"/>.
/// </summary>
public sealed class RematchHandlerTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromMinutes(3);

    private static OfferRematchHandler BuildOfferHandler(
        FakeClock clock, FakeRoomRepository rooms,
        RecordingTimeoutScheduler timeouts, IRateLimiter? limiter = null) =>
        new(rooms, new SingleGameRegistry(), clock, timeouts, limiter ?? new RecordingRateLimiter());

    private static AcceptRematchHandler BuildAcceptHandler(
        FakeClock clock, FakeRoomRepository rooms,
        RecordingTimeoutScheduler timeouts, IRateLimiter? limiter = null) =>
        new(rooms, new SingleGameRegistry(), clock, timeouts, limiter ?? new RecordingRateLimiter());

    private static RejectRematchHandler BuildRejectHandler(
        FakeClock clock, FakeRoomRepository rooms, IRateLimiter? limiter = null) =>
        new(rooms, new SingleGameRegistry(), clock, limiter ?? new RecordingRateLimiter());

    [Fact]
    public async Task OfferRematch_from_Ended_records_offer_no_timeout_scheduled()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        rooms.Seed(EndedRoom(clock));
        var handler = BuildOfferHandler(clock, rooms, timeouts);

        var result = await handler.HandleAsync(
            new OfferRematchCommand(
                RoomFactory.RoomCodeValue, RoomFactory.HostPlayerId, Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Effect.Should().Be(RematchOfferResult.OfferRecorded);
        timeouts.Scheduled.Should().BeEmpty();

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.Status.Should().Be(RoomStatus.AwaitingRematch);
        saved.RematchOffererRole.Should().Be(Role.Host);
    }

    [Fact]
    public async Task OfferRematch_dual_offer_under_lock_implicitly_accepts_and_schedules_timeout()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        rooms.Seed(EndedRoom(clock));
        var handler = BuildOfferHandler(clock, rooms, timeouts);

        var first = await handler.HandleAsync(
            new OfferRematchCommand(
                RoomFactory.RoomCodeValue, RoomFactory.HostPlayerId, Role.Host),
            CancellationToken.None);
        first.Value!.Effect.Should().Be(RematchOfferResult.OfferRecorded);

        var second = await handler.HandleAsync(
            new OfferRematchCommand(
                RoomFactory.RoomCodeValue, RoomFactory.ChallengerPlayerId, Role.Challenger),
            CancellationToken.None);

        second.Succeeded.Should().BeTrue();
        second.Value!.Effect.Should().Be(RematchOfferResult.ImplicitlyAccepted);
        timeouts.Scheduled.Should().HaveCount(1);

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.Status.Should().Be(RoomStatus.InProgress);
        // Sides swapped: host had X, now has O.
        saved.Host.Side.Should().Be(TicTacToeSides.O);
    }

    [Fact]
    public async Task OfferRematch_from_InProgress_returns_RematchInvalidState()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        rooms.Seed(RoomFactory.InProgress(clock.UtcNow, Budget));
        var handler = BuildOfferHandler(clock, rooms, timeouts);

        var result = await handler.HandleAsync(
            new OfferRematchCommand(
                RoomFactory.RoomCodeValue, RoomFactory.HostPlayerId, Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(PlatformErrors.RematchInvalidState);
    }

    [Fact]
    public async Task AcceptRematch_swaps_sides_and_starts_new_match()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        rooms.Seed(AwaitingRematchOfferedByHost(clock));
        var handler = BuildAcceptHandler(clock, rooms, timeouts);

        var result = await handler.HandleAsync(
            new AcceptRematchCommand(
                RoomFactory.RoomCodeValue, RoomFactory.ChallengerPlayerId, Role.Challenger),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        timeouts.Scheduled.Should().HaveCount(1);

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.Status.Should().Be(RoomStatus.InProgress);
        saved.Host.Side.Should().Be(TicTacToeSides.O);
        saved.Challenger!.Side.Should().Be(TicTacToeSides.X);
        saved.CurrentMatch!.Outcome.Should().BeNull();
    }

    [Fact]
    public async Task AcceptRematch_by_offerer_returns_NotResponder()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        rooms.Seed(AwaitingRematchOfferedByHost(clock));
        var handler = BuildAcceptHandler(clock, rooms, timeouts);

        var result = await handler.HandleAsync(
            new AcceptRematchCommand(
                RoomFactory.RoomCodeValue, RoomFactory.HostPlayerId, Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(PlatformErrors.RematchNotResponder);

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.Status.Should().Be(RoomStatus.AwaitingRematch);
    }

    [Fact]
    public async Task AcceptRematch_from_Ended_returns_InvalidState()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        rooms.Seed(EndedRoom(clock));
        var handler = BuildAcceptHandler(clock, rooms, timeouts);

        var result = await handler.HandleAsync(
            new AcceptRematchCommand(
                RoomFactory.RoomCodeValue, RoomFactory.ChallengerPlayerId, Role.Challenger),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(PlatformErrors.RematchInvalidState);
    }

    [Fact]
    public async Task RejectRematch_closes_room_and_clears_offerer()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        rooms.Seed(AwaitingRematchOfferedByHost(clock));
        var handler = BuildRejectHandler(clock, rooms);

        var result = await handler.HandleAsync(
            new RejectRematchCommand(
                RoomFactory.RoomCodeValue, RoomFactory.ChallengerPlayerId, Role.Challenger),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.Status.Should().Be(RoomStatus.Closed);
        saved.RematchOffererRole.Should().BeNull();
    }

    [Fact]
    public async Task RejectRematch_by_offerer_returns_NotResponder()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        rooms.Seed(AwaitingRematchOfferedByHost(clock));
        var handler = BuildRejectHandler(clock, rooms);

        var result = await handler.HandleAsync(
            new RejectRematchCommand(
                RoomFactory.RoomCodeValue, RoomFactory.HostPlayerId, Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(PlatformErrors.RematchNotResponder);
    }

    [Fact]
    public async Task Rematch_handlers_short_circuit_on_rate_limit()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        var limiter = new RecordingRateLimiter { AllowNext = false };
        rooms.Seed(AwaitingRematchOfferedByHost(clock));

        var offer = await BuildOfferHandler(clock, rooms, timeouts, limiter).HandleAsync(
            new OfferRematchCommand(
                RoomFactory.RoomCodeValue, RoomFactory.HostPlayerId, Role.Host),
            CancellationToken.None);
        var accept = await BuildAcceptHandler(clock, rooms, timeouts, limiter).HandleAsync(
            new AcceptRematchCommand(
                RoomFactory.RoomCodeValue, RoomFactory.ChallengerPlayerId, Role.Challenger),
            CancellationToken.None);
        var reject = await BuildRejectHandler(clock, rooms, limiter).HandleAsync(
            new RejectRematchCommand(
                RoomFactory.RoomCodeValue, RoomFactory.ChallengerPlayerId, Role.Challenger),
            CancellationToken.None);

        offer.Error.Should().Be(PlatformErrors.RateExceeded);
        accept.Error.Should().Be(PlatformErrors.RateExceeded);
        reject.Error.Should().Be(PlatformErrors.RateExceeded);

        // All three rate-limit checks used the same shared policy.
        limiter.Calls.Should().AllSatisfy(c =>
            c.Policy.Should().Be(SessionRateLimitPolicies.Rematch));
    }

    private static Room EndedRoom(FakeClock clock)
    {
        var room = RoomFactory.InProgress(clock.UtcNow, Budget);
        room.CurrentMatch!.Resign(TicTacToeSides.X);
        room.EndCurrentMatch();
        return room;
    }

    private static Room AwaitingRematchOfferedByHost(FakeClock clock)
    {
        var room = EndedRoom(clock);
        room.OfferRematch(Role.Host, new TicTacToeGameModule(), clock.UtcNow);
        return room;
    }
}
