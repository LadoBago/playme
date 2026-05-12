using FluentAssertions;
using PlayMe.Application.Commands.SubmitMove;
using PlayMe.Application.Dtos;
using PlayMe.Application.Errors;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Application.Time;
using PlayMe.Domain.Games.TicTacToe3x3;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Clock-aware behaviours added to the move pipeline in Sprint 2: the
/// handler now decrements the moving player's clock, re-schedules the
/// timeout entry on every accepted move, and converts a stale-clock
/// submission into a <c>Timeout</c> outcome without running the rules
/// engine.
/// </summary>
public sealed class SubmitMoveHandlerClockTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Accepting_a_move_decrements_only_the_movers_clock_and_reschedules_timeout()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        var room = RoomFactory.InProgress(clock.UtcNow, Budget);
        rooms.Seed(room);

        var handler = new SubmitMoveHandler(
            rooms,
            new SingleGameRegistry(),
            clock,
            new ClockService(),
            timeouts);

        clock.Advance(TimeSpan.FromSeconds(8));

        var result = await handler.HandleAsync(
            new SubmitMoveCommand(
                RoomFactory.RoomCodeValue,
                RoomFactory.HostPlayerId,
                Role.Host,
                new MoveDto(Cell: 0)),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.MatchEnded.Should().BeFalse();
        result.Value.TimedOut.Should().BeFalse();

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.CurrentMatch!.Clock.HostRemaining.Should().Be(TimeSpan.FromSeconds(52));
        saved.CurrentMatch.Clock.ChallengerRemaining.Should().Be(TimeSpan.FromSeconds(60));
        saved.CurrentMatch.Clock.ActivePlayer.Should().Be(Role.Challenger);

        timeouts.Scheduled.Should().HaveCount(1);
        timeouts.Scheduled[0].RoomCode.Should().Be(RoomFactory.RoomCodeValue);
        timeouts.Scheduled[0].Deadline.Should().Be(clock.UtcNow + Budget);
    }

    [Fact]
    public async Task Submission_when_active_clock_already_expired_yields_Timeout_outcome()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        rooms.Seed(RoomFactory.InProgress(clock.UtcNow, Budget));

        var handler = new SubmitMoveHandler(
            rooms,
            new SingleGameRegistry(),
            clock,
            new ClockService(),
            timeouts);

        clock.Advance(Budget + TimeSpan.FromSeconds(1));

        var result = await handler.HandleAsync(
            new SubmitMoveCommand(
                RoomFactory.RoomCodeValue,
                RoomFactory.HostPlayerId,
                Role.Host,
                new MoveDto(Cell: 0)),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.TimedOut.Should().BeTrue();
        result.Value.MatchEnded.Should().BeTrue();
        result.Value.AcceptedCell.Should().BeNull();

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.Status.Should().Be(RoomStatus.Ended);
        saved.CurrentMatch!.Outcome.Should().BeOfType<Domain.Platform.Timeout>()
            .Which.TimedOutSide.Should().Be(TicTacToeSides.X);

        timeouts.Cancelled.Should().Contain(RoomFactory.RoomCodeValue);
    }

    [Fact]
    public async Task Win_clears_timeout_schedule()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        rooms.Seed(RoomFactory.InProgress(clock.UtcNow, Budget));

        var handler = new SubmitMoveHandler(
            rooms,
            new SingleGameRegistry(),
            clock,
            new ClockService(),
            timeouts);

        // Drive the match to a quick X win: X(0), O(3), X(1), O(4), X(2).
        var moves = new (string PlayerId, Role Role, int Cell)[]
        {
            (RoomFactory.HostPlayerId, Role.Host, 0),
            (RoomFactory.ChallengerPlayerId, Role.Challenger, 3),
            (RoomFactory.HostPlayerId, Role.Host, 1),
            (RoomFactory.ChallengerPlayerId, Role.Challenger, 4),
            (RoomFactory.HostPlayerId, Role.Host, 2),
        };
        foreach (var (playerId, role, cell) in moves)
        {
            var ok = await handler.HandleAsync(
                new SubmitMoveCommand(
                    RoomFactory.RoomCodeValue,
                    playerId,
                    role,
                    new MoveDto(Cell: cell)),
                CancellationToken.None);
            ok.Succeeded.Should().BeTrue();
        }

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.Status.Should().Be(RoomStatus.Ended);
        saved.CurrentMatch!.Outcome.Should().BeOfType<Win>();

        timeouts.Cancelled.Should().Contain(RoomFactory.RoomCodeValue);
    }
}
