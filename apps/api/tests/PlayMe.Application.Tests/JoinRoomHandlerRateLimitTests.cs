using FluentAssertions;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Commands.JoinRoom;
using PlayMe.Application.Errors;
using PlayMe.Application.RateLimiting;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Per-room-code rate-limit behaviour for the join pipeline
/// (docs/security.md §5: 10 joins/hr per code). The handler must
/// short-circuit with the <c>errors.rate.exceeded</c> key before
/// acquiring the room lock — the whole point of the limit is to keep a
/// machine-rejoin flood off a leaked invite link.
/// </summary>
public sealed class JoinRoomHandlerRateLimitTests
{
    [Fact]
    public async Task Rate_exceeded_short_circuits_with_RateExceeded_key()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var rateLimiter = new RecordingRateLimiter { AllowNext = false };

        var handler = new JoinRoomHandler(
            rooms,
            new ThrowingPlayerIdGenerator(),
            new SingleGameRegistry(),
            clock,
            rateLimiter);

        var result = await handler.HandleAsync(
            new JoinRoomCommand(RoomFactory.RoomCodeValue, "Friend", Side: null),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(PlatformErrors.RateExceeded);

        // The limiter was consulted with the ByCode policy and the room
        // code — and the room was never even loaded.
        rateLimiter.Calls.Should().ContainSingle()
            .Which.Should().Be((JoinRateLimitPolicies.ByCode, RoomFactory.RoomCodeValue));
    }

    [Fact]
    public async Task Allowed_calls_partition_by_the_room_code()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var rateLimiter = new RecordingRateLimiter();

        var handler = new JoinRoomHandler(
            rooms,
            new ThrowingPlayerIdGenerator(),
            new SingleGameRegistry(),
            clock,
            rateLimiter);

        await handler.HandleAsync(
            new JoinRoomCommand(RoomFactory.RoomCodeValue, "Friend", Side: null),
            CancellationToken.None);

        rateLimiter.Calls.Should().ContainSingle()
            .Which.Should().Be((JoinRateLimitPolicies.ByCode, RoomFactory.RoomCodeValue));
    }

    private sealed class ThrowingPlayerIdGenerator : IPlayerIdGenerator
    {
        public PlayerId NewPlayerId() =>
            throw new InvalidOperationException(
                "JoinRoomHandlerRateLimitTests don't exercise player-id minting.");
    }
}
