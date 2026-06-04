using FluentAssertions;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Domain.Games.TicTacToe;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// <see cref="Room.EndCurrentMatch"/> updates the series scoreboard by
/// translating the just-concluded match's <see cref="Outcome"/> into a
/// role-keyed counter update (docs/platform.md §1 #13). The room
/// uses Host=X, Challenger=O via <see cref="RoomFactory.InProgress"/>.
/// </summary>
public sealed class RoomScoreTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromMinutes(3);

    [Fact]
    public void New_room_starts_with_zero_score()
    {
        var room = RoomFactory.InProgress(DateTimeOffset.UtcNow, Budget);
        room.SeriesScore.Should().Be(SeriesScore.Zero);
    }

    [Fact]
    public void Win_for_X_bumps_host_wins()
    {
        var room = RoomFactory.InProgress(DateTimeOffset.UtcNow, Budget);
        room.CurrentMatch!.ApplyAcceptedMove(
            newState: room.CurrentMatch.State,
            nextSideToMove: TicTacToeSides.O,
            nextActivePlayer: Role.Challenger,
            now: DateTimeOffset.UtcNow,
            ending: new Win(TicTacToeSides.X));

        room.EndCurrentMatch();

        room.SeriesScore.Should().Be(new SeriesScore(Host: 1, Challenger: 0, Draws: 0));
    }

    [Fact]
    public void Draw_bumps_the_draws_counter()
    {
        var room = RoomFactory.InProgress(DateTimeOffset.UtcNow, Budget);
        room.CurrentMatch!.ApplyAcceptedMove(
            newState: room.CurrentMatch.State,
            nextSideToMove: TicTacToeSides.O,
            nextActivePlayer: Role.Challenger,
            now: DateTimeOffset.UtcNow,
            ending: new Draw());

        room.EndCurrentMatch();

        room.SeriesScore.Should().Be(new SeriesScore(Host: 0, Challenger: 0, Draws: 1));
    }

    [Fact]
    public void Resign_credits_the_opponent_not_the_resigner()
    {
        // X (host) resigns -> Challenger gets the point.
        var room = RoomFactory.InProgress(DateTimeOffset.UtcNow, Budget);
        room.CurrentMatch!.Resign(TicTacToeSides.X);

        room.EndCurrentMatch();

        room.SeriesScore.Should().Be(new SeriesScore(Host: 0, Challenger: 1, Draws: 0));
    }

    [Fact]
    public void Timeout_credits_the_opponent_of_the_side_that_ran_out()
    {
        // O (challenger) times out -> Host gets the point.
        var room = RoomFactory.InProgress(DateTimeOffset.UtcNow, Budget);
        room.CurrentMatch!.ApplyTimeout(TicTacToeSides.O, DateTimeOffset.UtcNow);

        room.EndCurrentMatch();

        room.SeriesScore.Should().Be(new SeriesScore(Host: 1, Challenger: 0, Draws: 0));
    }
}
