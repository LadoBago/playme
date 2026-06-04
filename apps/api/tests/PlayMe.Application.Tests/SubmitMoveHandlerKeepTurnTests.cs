using System.Text.Json;
using FluentAssertions;
using PlayMe.Application.Commands.SubmitMove;
using PlayMe.Application.Dtos;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Application.Time;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Seam B (Sprint 10): <see cref="MoveResult.KeepTurn"/> lets a module
/// retain the mover's turn (sea battle's hit-shoots-again). Contract under
/// test — per docs/roadmap/sprint-10-sea-battle.md: the turn stays with
/// the caller, the caller's elapsed time is committed and their clock
/// keeps running, the no-move timeout is rescheduled against the caller's
/// new deadline, and a match-ending move ends the match regardless of the
/// flag. Default-flag behavior (strict alternation) is covered by the
/// existing SubmitMoveHandler suites.
/// </summary>
public sealed class SubmitMoveHandlerKeepTurnTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(60);

    private static Room KeepTurnRoom(FakeKeepTurnModule module, DateTimeOffset now)
    {
        var room = Room.Create(
            new RoomCode(RoomFactory.RoomCodeValue),
            module.Id,
            SideSelectionMode.HostPicksSpecific,
            new Player(
                new PlayerId(RoomFactory.HostPlayerId),
                DisplayName.Create("Host"),
                "first"),
            now,
            gameOptions: null);

        room.RegisterChallenger(
            new Player(
                new PlayerId(RoomFactory.ChallengerPlayerId),
                DisplayName.Create("Challenger"),
                Side: null),
            challengerPickedSide: null,
            module);

        room.MarkConnected(Role.Host);
        room.MarkConnected(Role.Challenger);
        room.TryStartMatch(module, Budget, now);
        return room;
    }

    private static SubmitMoveHandler Handler(
        FakeRoomRepository rooms, FakeClock clock, RecordingTimeoutScheduler timeouts) =>
        new(
            rooms,
            new StubModuleRegistry(new FakeKeepTurnModule(), new FakeKeepTurnMoveParser()),
            clock,
            new ClockService(),
            timeouts,
            new RecordingGraceScheduler(),
            new RecordingRateLimiter(),
            new RecordingAnalyticsClient());

    private static SubmitMoveCommand Move(bool keepTurn, bool win = false) =>
        new(
            RoomFactory.RoomCodeValue,
            RoomFactory.HostPlayerId,
            Role.Host,
            new MoveDto(JsonSerializer.SerializeToElement(new { keepTurn, win })));

    [Fact]
    public async Task KeepTurn_move_leaves_the_caller_on_the_move()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        var module = new FakeKeepTurnModule();
        rooms.Seed(KeepTurnRoom(module, clock.UtcNow));
        var handler = Handler(rooms, clock, timeouts);

        clock.Advance(TimeSpan.FromSeconds(8));
        var result = await handler.HandleAsync(Move(keepTurn: true), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.CurrentMatch!.SideToMove.Should().Be("first");
        saved.CurrentMatch.Clock.ActivePlayer.Should().Be(Role.Host);
        result.Value!.Room.CurrentMatch!.SideToMove.Should().Be("first");
    }

    [Fact]
    public async Task KeepTurn_move_commits_elapsed_time_and_keeps_the_callers_clock_running()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        var module = new FakeKeepTurnModule();
        rooms.Seed(KeepTurnRoom(module, clock.UtcNow));
        var handler = Handler(rooms, clock, timeouts);

        clock.Advance(TimeSpan.FromSeconds(8));
        await handler.HandleAsync(Move(keepTurn: true), CancellationToken.None);

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        var matchClock = saved!.CurrentMatch!.Clock;

        // The 8 elapsed seconds are charged to the mover; the opponent is
        // untouched; LastTickAt advances so the mover's clock keeps
        // draining from "now" into their retained turn.
        matchClock.HostRemaining.Should().Be(TimeSpan.FromSeconds(52));
        matchClock.ChallengerRemaining.Should().Be(Budget);
        matchClock.LastTickAt.Should().Be(clock.UtcNow);

        // And the no-move timeout is rescheduled for the caller's new deadline.
        timeouts.Scheduled.Should().HaveCount(1);
        timeouts.Scheduled[0].Deadline.Should().Be(clock.UtcNow + TimeSpan.FromSeconds(52));
    }

    [Fact]
    public async Task Two_keepTurn_moves_then_a_normal_move_flips_to_the_opponent()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        var module = new FakeKeepTurnModule();
        rooms.Seed(KeepTurnRoom(module, clock.UtcNow));
        var handler = Handler(rooms, clock, timeouts);

        clock.Advance(TimeSpan.FromSeconds(3));
        (await handler.HandleAsync(Move(keepTurn: true), CancellationToken.None))
            .Succeeded.Should().BeTrue();
        clock.Advance(TimeSpan.FromSeconds(4));
        (await handler.HandleAsync(Move(keepTurn: true), CancellationToken.None))
            .Succeeded.Should().BeTrue();
        clock.Advance(TimeSpan.FromSeconds(5));
        (await handler.HandleAsync(Move(keepTurn: false), CancellationToken.None))
            .Succeeded.Should().BeTrue();

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.CurrentMatch!.SideToMove.Should().Be("second");
        saved.CurrentMatch.Clock.ActivePlayer.Should().Be(Role.Challenger);
        saved.CurrentMatch.Clock.HostRemaining.Should().Be(TimeSpan.FromSeconds(48));
        saved.CurrentMatch.Clock.ChallengerRemaining.Should().Be(Budget);
        saved.CurrentMatch.MoveCount.Should().Be(3);
    }

    [Fact]
    public async Task Opponent_cannot_move_during_a_retained_turn()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        var module = new FakeKeepTurnModule();
        rooms.Seed(KeepTurnRoom(module, clock.UtcNow));
        var handler = Handler(rooms, clock, timeouts);

        await handler.HandleAsync(Move(keepTurn: true), CancellationToken.None);

        var challengerMove = new SubmitMoveCommand(
            RoomFactory.RoomCodeValue,
            RoomFactory.ChallengerPlayerId,
            Role.Challenger,
            new MoveDto(JsonSerializer.SerializeToElement(new { keepTurn = false, win = false })));
        var result = await handler.HandleAsync(challengerMove, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("errors.move.notYourTurn");
    }

    [Fact]
    public async Task Match_ending_move_ends_the_match_regardless_of_keepTurn()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        var module = new FakeKeepTurnModule();
        rooms.Seed(KeepTurnRoom(module, clock.UtcNow));
        var handler = Handler(rooms, clock, timeouts);

        var result = await handler.HandleAsync(
            Move(keepTurn: true, win: true), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.MatchEnded.Should().BeTrue();
        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.Status.Should().Be(RoomStatus.Ended);
        saved.CurrentMatch!.Outcome.Should().BeOfType<Win>()
            .Which.WinningSide.Should().Be("first");
        timeouts.Cancelled.Should().Contain(RoomFactory.RoomCodeValue);
    }
}
