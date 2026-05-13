using FluentAssertions;
using PlayMe.Application.Mapping;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Pins the wire contract for <see cref="Application.Dtos.ClockSnapshotDto"/>:
/// <c>hostMs</c>/<c>challengerMs</c> are effective values at
/// <c>serverNowAt</c>, not raw stored values at <c>lastTickAt</c>. A
/// mid-turn HTTP snapshot (e.g. <c>getRoom</c>) must already have the
/// active player's elapsed time subtracted; otherwise the client would
/// "top up" the clock on a page refresh and then jump to zero when the
/// timeout sweeper fires.
/// </summary>
public sealed class RoomMapperClockTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 5, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToClockSnapshotDto_extrapolates_active_side_when_match_in_progress()
    {
        var clock = MatchClock.Start(TimeSpan.FromSeconds(60), Role.Host, Start);
        var now = Start + TimeSpan.FromSeconds(15);

        var dto = RoomMapper.ToClockSnapshotDto(clock, now, matchEnded: false);

        // Host is active; 15s elapsed → effective 45s.
        dto.HostMs.Should().Be(45_000);
        dto.ChallengerMs.Should().Be(60_000);
        dto.ServerNowAt.Should().Be(now);
    }

    [Fact]
    public void ToClockSnapshotDto_freezes_stored_values_when_match_ended()
    {
        var clock = MatchClock.Start(TimeSpan.FromSeconds(60), Role.Host, Start);
        var now = Start + TimeSpan.FromSeconds(15);

        // Match-ended snapshots must NOT decrement the (loser-side) active
        // clock; ApplyAcceptedMove flips active to the loser as part of
        // every move, so an ended match has the loser nominally "active".
        var dto = RoomMapper.ToClockSnapshotDto(clock, now, matchEnded: true);

        dto.HostMs.Should().Be(60_000);
        dto.ChallengerMs.Should().Be(60_000);
    }

    [Fact]
    public void ToClockSnapshotDto_floors_at_zero_past_deadline()
    {
        var clock = MatchClock.Start(TimeSpan.FromSeconds(60), Role.Challenger, Start);
        var now = Start + TimeSpan.FromSeconds(90);

        var dto = RoomMapper.ToClockSnapshotDto(clock, now, matchEnded: false);

        dto.ChallengerMs.Should().Be(0);
        dto.HostMs.Should().Be(60_000);
    }
}
