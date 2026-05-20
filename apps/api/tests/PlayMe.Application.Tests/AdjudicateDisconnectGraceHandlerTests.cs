using FluentAssertions;
using PlayMe.Application.Commands.AdjudicateDisconnectGrace;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Application.Time;
using PlayMe.Domain.Games.TicTacToe3x3;
using PlayMe.Domain.Platform;
using PlayMe.Infrastructure.Security;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Behaviour of <see cref="AdjudicateDisconnectGraceHandler"/>: under the
/// room lock (held by the sweeper), the handler re-verifies every
/// precondition and either ends the match with <see cref="Disconnect"/>
/// or short-circuits silently if the entry has been invalidated.
/// </summary>
public sealed class AdjudicateDisconnectGraceHandlerTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(60);

    private static AdjudicateDisconnectGraceHandler BuildHandler(
        FakeClock clock,
        FakeRoomRepository rooms,
        RecordingTimeoutScheduler timeouts,
        RecordingAnalyticsClient? analytics = null) =>
        new(rooms,
            new SingleGameRegistry(),
            clock,
            new ClockService(),
            timeouts,
            new RoomCodeRedactor(),
            analytics ?? new RecordingAnalyticsClient(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AdjudicateDisconnectGraceHandler>.Instance);

    [Fact]
    public async Task Adjudicates_when_disconnected_role_is_active_and_clock_not_expired()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        var room = RoomFactory.InProgress(clock.UtcNow, Budget);
        room.MarkDisconnected(Role.Host); // X is host, X moves first → host is active
        rooms.Seed(room);
        var handler = BuildHandler(clock, rooms, timeouts);

        // Advance clock 10s into the active player's turn — still well
        // within the 60s budget, so the clock hasn't run out.
        clock.Advance(TimeSpan.FromSeconds(10));

        var result = await handler.HandleAsync(
            new AdjudicateDisconnectGraceCommand(RoomFactory.RoomCodeValue, Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.MatchEnded.Should().BeTrue();
        result.Value.Room.Should().NotBeNull();

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.Status.Should().Be(RoomStatus.Ended);
        saved.CurrentMatch!.Outcome.Should().BeOfType<Disconnect>()
            .Which.LosingSide.Should().Be(TicTacToeSides.X);

        // Series scoreboard credits the opponent.
        saved.SeriesScore.Should().Be(new SeriesScore(Host: 0, Challenger: 1, Draws: 0));
        timeouts.Cancelled.Should().Contain(RoomFactory.RoomCodeValue);
    }

    [Fact]
    public async Task Drops_when_role_has_reconnected_before_adjudication()
    {
        // Disconnect → schedule → reconnect race. Reconnect lands first
        // (HostConnected flipped back to true); the queued entry fires
        // and must short-circuit.
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        var room = RoomFactory.InProgress(clock.UtcNow, Budget);
        room.MarkDisconnected(Role.Host);
        room.MarkConnected(Role.Host);
        rooms.Seed(room);
        var handler = BuildHandler(clock, rooms, timeouts);

        var result = await handler.HandleAsync(
            new AdjudicateDisconnectGraceCommand(RoomFactory.RoomCodeValue, Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.MatchEnded.Should().BeFalse();
        result.Value.Room.Should().BeNull();

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.Status.Should().Be(RoomStatus.InProgress);
    }

    [Fact]
    public async Task Drops_when_turn_has_flipped_away_from_disconnected_role()
    {
        // Race: the scheduled entry is for the still-active host, but by
        // the time the sweeper fires, the host has somehow yielded the
        // turn (reconnect → move → re-disconnect would do this in
        // practice). With the active player no longer matching the
        // entry's role, adjudication drops.
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        var room = RoomFactory.InProgress(clock.UtcNow, Budget);
        room.MarkDisconnected(Role.Host);
        // Flip the active player without moving — directly via the
        // domain method's effect; in production this would come from
        // an accepted move.
        room.CurrentMatch!.ApplyAcceptedMove(
            newState: room.CurrentMatch.State,
            nextSideToMove: TicTacToeSides.O,
            nextActivePlayer: Role.Challenger,
            now: clock.UtcNow,
            ending: null);
        rooms.Seed(room);
        var handler = BuildHandler(clock, rooms, timeouts);

        var result = await handler.HandleAsync(
            new AdjudicateDisconnectGraceCommand(RoomFactory.RoomCodeValue, Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.MatchEnded.Should().BeFalse();

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.Status.Should().Be(RoomStatus.InProgress);
    }

    [Fact]
    public async Task Yields_to_chess_clock_when_active_player_clock_already_expired()
    {
        // The timeout sweeper will catch this case as Timeout, not
        // Disconnect — adjudication must drop and let the timeout
        // sweeper produce the honest outcome.
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        var room = RoomFactory.InProgress(clock.UtcNow, Budget);
        room.MarkDisconnected(Role.Host);
        rooms.Seed(room);
        var handler = BuildHandler(clock, rooms, timeouts);

        clock.Advance(Budget + TimeSpan.FromSeconds(1));

        var result = await handler.HandleAsync(
            new AdjudicateDisconnectGraceCommand(RoomFactory.RoomCodeValue, Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.MatchEnded.Should().BeFalse();

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.Status.Should().Be(RoomStatus.InProgress);
    }

    [Fact]
    public async Task Drops_when_room_already_ended()
    {
        // Match ended before the sweeper got around to processing the
        // grace entry — silently drop. No re-broadcast of MatchEnded.
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        var room = RoomFactory.InProgress(clock.UtcNow, Budget);
        room.CurrentMatch!.Resign(TicTacToeSides.X);
        room.EndCurrentMatch();
        rooms.Seed(room);
        var handler = BuildHandler(clock, rooms, timeouts);

        var result = await handler.HandleAsync(
            new AdjudicateDisconnectGraceCommand(RoomFactory.RoomCodeValue, Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.MatchEnded.Should().BeFalse();

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        // Outcome stays as the original Resign — the abandon adjudication
        // didn't overwrite a finished match.
        saved!.CurrentMatch!.Outcome.Should().BeOfType<Resign>();
    }
}
