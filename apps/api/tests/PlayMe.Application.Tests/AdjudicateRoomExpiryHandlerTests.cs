using FluentAssertions;
using PlayMe.Application.Commands.AdjudicateRoomExpiry;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Domain.Games.TicTacToe;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Behaviour of <see cref="AdjudicateRoomExpiryHandler"/>: under the
/// room lock (held by the sweeper), the handler fires
/// <c>room_expired</c> for unjoined rooms and drops the entry silently
/// when the room moved to <see cref="RoomStatus.InProgress"/> between
/// schedule and sweep.
/// </summary>
public sealed class AdjudicateRoomExpiryHandlerTests
{
    private const string GameIdValue = "tictactoe";

    private static AdjudicateRoomExpiryHandler BuildHandler(
        FakeRoomRepository rooms,
        RecordingAnalyticsClient analytics) =>
        new(rooms, analytics);

    [Fact]
    public async Task Reaped_room_fires_room_expired_analytics()
    {
        // Common case: by the time the sweeper acquires the lock,
        // Redis has already deleted the room key (TTL = expiry deadline).
        var rooms = new FakeRoomRepository();
        var analytics = new RecordingAnalyticsClient();
        var handler = BuildHandler(rooms, analytics);

        var result = await handler.HandleAsync(
            new AdjudicateRoomExpiryCommand(RoomFactory.RoomCodeValue, GameIdValue),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Expired.Should().BeTrue();
        analytics.Events.Should().ContainSingle()
            .Which.Should().Match<(string Event, string DistinctId, IReadOnlyDictionary<string, object?> Properties)>(e =>
                e.Event == "room_expired"
                && e.DistinctId == RoomFactory.RoomCodeValue
                && (string)e.Properties["gameId"]! == GameIdValue);
    }

    [Fact]
    public async Task WaitingForOpponent_room_still_loaded_also_fires_event()
    {
        // Less common but real: the sweeper acquired the lock before
        // Redis fully evicted the key. Status is still WaitingForOpponent
        // because nobody joined.
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        rooms.Seed(RoomFactory.WaitingForOpponent(clock.UtcNow));
        var analytics = new RecordingAnalyticsClient();
        var handler = BuildHandler(rooms, analytics);

        var result = await handler.HandleAsync(
            new AdjudicateRoomExpiryCommand(RoomFactory.RoomCodeValue, GameIdValue),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Expired.Should().BeTrue();
        analytics.Events.Should().ContainSingle();
    }

    [Fact]
    public async Task Joined_late_room_drops_silently()
    {
        // Race: between ScheduleAsync and the sweep, a challenger
        // joined and the match started. The handler MUST NOT fire
        // room_expired — the room is no longer "unjoined."
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        rooms.Seed(RoomFactory.InProgress(clock.UtcNow, TimeSpan.FromSeconds(60)));
        var analytics = new RecordingAnalyticsClient();
        var handler = BuildHandler(rooms, analytics);

        var result = await handler.HandleAsync(
            new AdjudicateRoomExpiryCommand(RoomFactory.RoomCodeValue, GameIdValue),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Expired.Should().BeFalse();
        analytics.Events.Should().BeEmpty();
    }
}
