using FluentAssertions;
using PlayMe.Application.Abandon;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Pure-policy unit tests for the tiered grace decision
/// (docs/platform-and-games.md §1 #7). The integration paths — when
/// callers actually invoke the policy — live in <c>PresenceHandlerTests</c>,
/// <c>SubmitMoveHandler*Tests</c>, and <c>AdjudicateDisconnectGraceHandlerTests</c>.
/// </summary>
public sealed class GraceSchedulingPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OneMin_budget_yields_no_deadline()
    {
        // ≤ 1 min: no grace tier — the chess-clock timeout catches the abandon.
        GraceSchedulingPolicy.ComputeDeadline(
            TimeSpan.FromMinutes(1),
            disconnectedRemaining: TimeSpan.FromSeconds(45),
            now: Now).Should().BeNull();
    }

    [Fact]
    public void ThreeMin_budget_with_plenty_remaining_schedules_60s_out()
    {
        var deadline = GraceSchedulingPolicy.ComputeDeadline(
            TimeSpan.FromMinutes(3),
            disconnectedRemaining: TimeSpan.FromMinutes(2),
            now: Now);

        deadline.Should().Be(Now + TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void ThreeMin_budget_with_remaining_at_or_below_grace_yields_no_deadline()
    {
        // Equal-to-grace: chess clock would expire before grace fires, so skip the schedule.
        GraceSchedulingPolicy.ComputeDeadline(
            TimeSpan.FromMinutes(3),
            disconnectedRemaining: TimeSpan.FromSeconds(60),
            now: Now).Should().BeNull();

        // Strictly below: same reasoning.
        GraceSchedulingPolicy.ComputeDeadline(
            TimeSpan.FromMinutes(3),
            disconnectedRemaining: TimeSpan.FromSeconds(45),
            now: Now).Should().BeNull();
    }

    [Fact]
    public void TenMin_budget_uses_90s_tier()
    {
        var deadline = GraceSchedulingPolicy.ComputeDeadline(
            TimeSpan.FromMinutes(10),
            disconnectedRemaining: TimeSpan.FromMinutes(5),
            now: Now);

        deadline.Should().Be(Now + TimeSpan.FromSeconds(90));
    }

    [Fact]
    public void TenMin_budget_with_remaining_below_grace_yields_no_deadline()
    {
        GraceSchedulingPolicy.ComputeDeadline(
            TimeSpan.FromMinutes(10),
            disconnectedRemaining: TimeSpan.FromSeconds(89),
            now: Now).Should().BeNull();
    }

    [Fact]
    public void FiveMin_budget_at_the_boundary_is_still_60s_tier()
    {
        // > 1 min, ≤ 5 min boundary — the 5-min mark is included in the 60s tier.
        GraceSchedulingPolicy.ComputeDeadline(
            TimeSpan.FromMinutes(5),
            disconnectedRemaining: TimeSpan.FromMinutes(4),
            now: Now).Should().Be(Now + TimeSpan.FromSeconds(60));

        // 5 min + 1 ms flips into the 90s tier.
        GraceSchedulingPolicy.ComputeDeadline(
            TimeSpan.FromMinutes(5) + TimeSpan.FromMilliseconds(1),
            disconnectedRemaining: TimeSpan.FromMinutes(4),
            now: Now).Should().Be(Now + TimeSpan.FromSeconds(90));
    }
}
