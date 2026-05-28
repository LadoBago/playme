using FluentAssertions;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Domain.Games.TicTacToe;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// State-machine behaviour of <see cref="Room.TryExit"/>
/// (docs/state.md §2.4). Valid in <see cref="RoomStatus.Ended"/> and
/// <see cref="RoomStatus.AwaitingRematch"/>; idempotent on <see cref="RoomStatus.Closed"/>;
/// returns false (no transition) otherwise.
/// </summary>
public sealed class RoomExitTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(60);

    [Fact]
    public void Exit_from_Ended_transitions_to_Closed()
    {
        var room = EndedRoom();

        var legal = room.TryExit(out var transitioned);

        legal.Should().BeTrue();
        transitioned.Should().BeTrue();
        room.Status.Should().Be(RoomStatus.Closed);
    }

    [Fact]
    public void Exit_idempotent_on_Closed_room_reports_no_transition()
    {
        var room = EndedRoom();
        room.TryExit(out _); // first call closes it.

        var legal = room.TryExit(out var transitioned);

        legal.Should().BeTrue();
        transitioned.Should().BeFalse();
        room.Status.Should().Be(RoomStatus.Closed);
    }

    [Fact]
    public void Exit_from_InProgress_returns_false_and_leaves_status_unchanged()
    {
        var room = RoomFactory.InProgress(DateTimeOffset.UtcNow, Budget);

        var legal = room.TryExit(out var transitioned);

        legal.Should().BeFalse();
        transitioned.Should().BeFalse();
        room.Status.Should().Be(RoomStatus.InProgress);
    }

    [Fact]
    public void Exit_from_WaitingForOpponent_returns_false_and_leaves_status_unchanged()
    {
        var room = Room.Create(
            new RoomCode(RoomFactory.RoomCodeValue),
            TicTacToeGameModule.GameId,
            SideSelectionMode.HostPicksSpecific,
            new Player(
                new PlayerId(RoomFactory.HostPlayerId),
                DisplayName.Create("Host"),
                TicTacToeSides.X),
            DateTimeOffset.UtcNow);

        var legal = room.TryExit(out var transitioned);

        legal.Should().BeFalse();
        transitioned.Should().BeFalse();
        room.Status.Should().Be(RoomStatus.WaitingForOpponent);
    }

    private static Room EndedRoom()
    {
        var room = RoomFactory.InProgress(DateTimeOffset.UtcNow, Budget);
        room.CurrentMatch!.Resign(TicTacToeSides.X);
        room.EndCurrentMatch();
        return room;
    }
}
