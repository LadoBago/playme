using FluentAssertions;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Domain.Games.TicTacToe3x3;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// State-machine behaviour of <see cref="Room.Exit"/>
/// (docs/state.md §2.4). Valid in <see cref="RoomStatus.Ended"/> and
/// <see cref="RoomStatus.AwaitingRematch"/>; idempotent on <see cref="RoomStatus.Closed"/>;
/// throws otherwise.
/// </summary>
public sealed class RoomExitTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(60);

    [Fact]
    public void Exit_from_Ended_transitions_to_Closed_and_returns_true()
    {
        var room = EndedRoom();

        var transitioned = room.Exit();

        transitioned.Should().BeTrue();
        room.Status.Should().Be(RoomStatus.Closed);
    }

    [Fact]
    public void Exit_idempotent_on_Closed_room_returns_false()
    {
        var room = EndedRoom();
        room.Exit(); // first call closes it.

        var second = room.Exit();

        second.Should().BeFalse();
        room.Status.Should().Be(RoomStatus.Closed);
    }

    [Fact]
    public void Exit_from_InProgress_throws()
    {
        var room = RoomFactory.InProgress(DateTimeOffset.UtcNow, Budget);

        var act = () => room.Exit();

        act.Should().Throw<DomainException>();
        room.Status.Should().Be(RoomStatus.InProgress);
    }

    [Fact]
    public void Exit_from_WaitingForOpponent_throws()
    {
        var room = Room.Create(
            new RoomCode(RoomFactory.RoomCodeValue),
            TicTacToe3x3GameModule.GameId,
            SideSelectionMode.HostPicksSpecific,
            new Player(
                new PlayerId(RoomFactory.HostPlayerId),
                DisplayName.Create("Host"),
                TicTacToeSides.X),
            DateTimeOffset.UtcNow);

        var act = () => room.Exit();

        act.Should().Throw<DomainException>();
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
