using FluentAssertions;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Value-object behaviour of <see cref="SeriesScore"/>. The translation from
/// <see cref="Outcome"/> subtype to score update lives on
/// <see cref="Room.EndCurrentMatch"/> (and is covered by
/// <see cref="RoomScoreTests"/>); this file pins the primitive operations.
/// </summary>
public sealed class SeriesScoreTests
{
    [Fact]
    public void Zero_starts_at_all_zeroes()
    {
        SeriesScore.Zero.Should().Be(new SeriesScore(0, 0, 0));
        SeriesScore.Zero.TotalMatches.Should().Be(0);
    }

    [Fact]
    public void WithWin_bumps_the_winning_role_only()
    {
        var afterHostWin = SeriesScore.Zero.WithWin(Role.Host);
        afterHostWin.Should().Be(new SeriesScore(1, 0, 0));

        var afterBothWins = afterHostWin.WithWin(Role.Challenger);
        afterBothWins.Should().Be(new SeriesScore(1, 1, 0));
        afterBothWins.TotalMatches.Should().Be(2);
    }

    [Fact]
    public void WithDraw_bumps_only_the_draws_counter()
    {
        var afterDraw = SeriesScore.Zero.WithDraw().WithDraw();
        afterDraw.Should().Be(new SeriesScore(0, 0, 2));
        afterDraw.TotalMatches.Should().Be(2);
    }

    [Fact]
    public void Score_records_are_immutable_under_With()
    {
        var original = SeriesScore.Zero;
        _ = original.WithWin(Role.Host).WithDraw();
        original.Should().Be(SeriesScore.Zero);
    }
}
