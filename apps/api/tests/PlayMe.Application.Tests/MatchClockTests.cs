using FluentAssertions;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Lazy-clock arithmetic per state.md §2.2. The fixture pins a
/// deterministic <c>startedAt</c> and walks the active player's clock
/// forward with synthetic <c>now</c> values rather than the system clock.
/// </summary>
public sealed class MatchClockTests
{
    private static readonly DateTimeOffset StartedAt =
        new(2026, 5, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Start_initialises_equal_budgets_for_both_sides()
    {
        var clock = MatchClock.Start(TimeSpan.FromSeconds(60), Role.Host, StartedAt);

        clock.HostRemaining.Should().Be(TimeSpan.FromSeconds(60));
        clock.ChallengerRemaining.Should().Be(TimeSpan.FromSeconds(60));
        clock.ActivePlayer.Should().Be(Role.Host);
        clock.LastTickAt.Should().Be(StartedAt);
    }

    [Fact]
    public void EffectiveRemaining_decreases_only_for_active_player()
    {
        var clock = MatchClock.Start(TimeSpan.FromSeconds(60), Role.Host, StartedAt);
        var now = StartedAt + TimeSpan.FromSeconds(10);

        clock.EffectiveRemaining(Role.Host, now).Should().Be(TimeSpan.FromSeconds(50));
        clock.EffectiveRemaining(Role.Challenger, now).Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void EffectiveRemaining_floors_at_zero()
    {
        var clock = MatchClock.Start(TimeSpan.FromSeconds(60), Role.Host, StartedAt);
        var now = StartedAt + TimeSpan.FromSeconds(75);

        clock.EffectiveRemaining(Role.Host, now).Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void AfterMove_subtracts_elapsed_from_mover_and_flips_active_player()
    {
        var clock = MatchClock.Start(TimeSpan.FromSeconds(60), Role.Host, StartedAt);
        var now = StartedAt + TimeSpan.FromSeconds(7);

        var next = clock.AfterMove(Role.Challenger, now);

        next.HostRemaining.Should().Be(TimeSpan.FromSeconds(53));
        next.ChallengerRemaining.Should().Be(TimeSpan.FromSeconds(60));
        next.ActivePlayer.Should().Be(Role.Challenger);
        next.LastTickAt.Should().Be(now);
    }

    [Fact]
    public void AfterMove_chained_decrements_the_correct_side_each_time()
    {
        var clock = MatchClock.Start(TimeSpan.FromSeconds(60), Role.Host, StartedAt);

        // Host takes 5s, then Challenger takes 8s.
        clock = clock.AfterMove(Role.Challenger, StartedAt + TimeSpan.FromSeconds(5));
        clock = clock.AfterMove(Role.Host, StartedAt + TimeSpan.FromSeconds(13));

        clock.HostRemaining.Should().Be(TimeSpan.FromSeconds(55));
        clock.ChallengerRemaining.Should().Be(TimeSpan.FromSeconds(52));
        clock.ActivePlayer.Should().Be(Role.Host);
    }

    [Fact]
    public void AfterTimeout_zeroes_active_side_only()
    {
        var clock = MatchClock.Start(TimeSpan.FromSeconds(60), Role.Challenger, StartedAt);
        var now = StartedAt + TimeSpan.FromSeconds(70);

        var next = clock.AfterTimeout(now);

        next.ChallengerRemaining.Should().Be(TimeSpan.Zero);
        next.HostRemaining.Should().Be(TimeSpan.FromSeconds(60));
        next.ActivePlayer.Should().Be(Role.Challenger);
        next.LastTickAt.Should().Be(now);
    }

    [Fact]
    public void ActivePlayerDeadline_lands_at_lastTick_plus_active_remaining()
    {
        var clock = MatchClock.Start(TimeSpan.FromSeconds(60), Role.Host, StartedAt);

        clock.ActivePlayerDeadline().Should().Be(StartedAt + TimeSpan.FromSeconds(60));
    }
}
