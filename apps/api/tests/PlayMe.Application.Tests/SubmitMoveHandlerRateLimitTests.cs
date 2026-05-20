using System.Text.Json;
using FluentAssertions;
using PlayMe.Application.Commands.SubmitMove;
using PlayMe.Application.Dtos;
using PlayMe.Application.Errors;
using PlayMe.Application.RateLimiting;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Application.Time;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Per-session rate-limit behaviour for the move pipeline
/// (docs/security.md §5: 60 moves/min/session, surviving SignalR
/// reconnects). The handler must short-circuit with the
/// <c>errors.rate.exceeded</c> key before acquiring the room lock — the
/// whole point of the limit is to keep a flood off the contention path.
/// </summary>
public sealed class SubmitMoveHandlerRateLimitTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Rate_exceeded_short_circuits_with_RateExceeded_key()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        var rateLimiter = new RecordingRateLimiter { AllowNext = false };
        rooms.Seed(RoomFactory.InProgress(clock.UtcNow, Budget));

        var handler = new SubmitMoveHandler(
            rooms,
            new SingleGameRegistry(),
            clock,
            new ClockService(),
            timeouts,
            new RecordingGraceScheduler(),
            rateLimiter,
            new RecordingAnalyticsClient());

        var result = await handler.HandleAsync(
            new SubmitMoveCommand(
                RoomFactory.RoomCodeValue,
                RoomFactory.HostPlayerId,
                Role.Host,
                new MoveDto(JsonSerializer.SerializeToElement(new { cell = 0 }))),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(PlatformErrors.RateExceeded);

        // The limiter was consulted with the SubmitMove policy and the
        // caller's playerId — and the room was never even loaded.
        rateLimiter.Calls.Should().ContainSingle()
            .Which.Should().Be((SessionRateLimitPolicies.SubmitMove, RoomFactory.HostPlayerId));
        timeouts.Scheduled.Should().BeEmpty();
        timeouts.Cancelled.Should().BeEmpty();
    }

    [Fact]
    public async Task Allowed_calls_pass_the_subject_key_through()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        var rateLimiter = new RecordingRateLimiter();
        rooms.Seed(RoomFactory.InProgress(clock.UtcNow, Budget));

        var handler = new SubmitMoveHandler(
            rooms,
            new SingleGameRegistry(),
            clock,
            new ClockService(),
            timeouts,
            new RecordingGraceScheduler(),
            rateLimiter,
            new RecordingAnalyticsClient());

        await handler.HandleAsync(
            new SubmitMoveCommand(
                RoomFactory.RoomCodeValue,
                RoomFactory.HostPlayerId,
                Role.Host,
                new MoveDto(JsonSerializer.SerializeToElement(new { cell = 0 }))),
            CancellationToken.None);

        rateLimiter.Calls.Should().ContainSingle()
            .Which.Subject.Should().Be(RoomFactory.HostPlayerId);
    }
}
