using FluentAssertions;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Domain.Games.TicTacToe;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Domain state-machine + side-swap behaviour of the rematch handshake
/// (docs/platform.md §1 #10 / #15). Per-handler authorization
/// and rate-limit behaviour live in the dedicated handler tests.
/// </summary>
public sealed class RoomRematchTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromMinutes(3);
    private static readonly TicTacToeGameModule Module = new();

    [Fact]
    public void OfferRematch_from_Ended_records_offerer_and_flips_to_AwaitingRematch()
    {
        var room = EndedRoom();

        var accepted = room.TryOfferRematch(Role.Host, Module, DateTimeOffset.UtcNow, out var effect);

        accepted.Should().BeTrue();
        effect.Should().Be(RematchOfferResult.OfferRecorded);
        room.Status.Should().Be(RoomStatus.AwaitingRematch);
        room.RematchOffererRole.Should().Be(Role.Host);
    }

    [Fact]
    public void OfferRematch_from_AwaitingRematch_with_different_caller_implicitly_accepts()
    {
        // Race: host offered, then before MatchStarted fires, challenger
        // also clicks "Offer rematch" — the second call lands inside the
        // lock and resolves as accept rather than producing two offers.
        var room = EndedRoom();
        room.TryOfferRematch(Role.Host, Module, DateTimeOffset.UtcNow, out _);

        var accepted = room.TryOfferRematch(
            Role.Challenger, Module, DateTimeOffset.UtcNow, out var effect);

        accepted.Should().BeTrue();
        effect.Should().Be(RematchOfferResult.ImplicitlyAccepted);
        room.Status.Should().Be(RoomStatus.InProgress);
        room.RematchOffererRole.Should().BeNull();
        room.CurrentMatch.Should().NotBeNull();
    }

    [Fact]
    public void Duplicate_offer_from_same_caller_is_rejected()
    {
        var room = EndedRoom();
        room.TryOfferRematch(Role.Host, Module, DateTimeOffset.UtcNow, out _);

        var accepted = room.TryOfferRematch(Role.Host, Module, DateTimeOffset.UtcNow, out _);

        accepted.Should().BeFalse();
        room.Status.Should().Be(RoomStatus.AwaitingRematch);
        room.RematchOffererRole.Should().Be(Role.Host);
    }

    [Fact]
    public void AcceptRematch_swaps_sides_and_starts_new_match()
    {
        var room = EndedRoom();
        room.Host.Side.Should().Be(TicTacToeSides.X);
        room.Challenger!.Side.Should().Be(TicTacToeSides.O);
        room.TryOfferRematch(Role.Host, Module, DateTimeOffset.UtcNow, out _);

        room.AcceptRematch(Role.Challenger, Module, DateTimeOffset.UtcNow);

        room.Status.Should().Be(RoomStatus.InProgress);
        room.Host.Side.Should().Be(TicTacToeSides.O);
        room.Challenger!.Side.Should().Be(TicTacToeSides.X);
        // X moves first in tic-tac-toe; after the swap that's the challenger.
        room.CurrentMatch!.SideToMove.Should().Be(TicTacToeSides.X);
        room.CurrentMatch.Clock.ActivePlayer.Should().Be(Role.Challenger);
        room.RematchOffererRole.Should().BeNull();
    }

    [Fact]
    public void AcceptRematch_by_the_offerer_throws()
    {
        var room = EndedRoom();
        room.TryOfferRematch(Role.Host, Module, DateTimeOffset.UtcNow, out _);

        var act = () => room.AcceptRematch(Role.Host, Module, DateTimeOffset.UtcNow);

        act.Should().Throw<DomainException>();
        room.Status.Should().Be(RoomStatus.AwaitingRematch);
    }

    [Fact]
    public void RejectRematch_transitions_to_Closed()
    {
        var room = EndedRoom();
        room.TryOfferRematch(Role.Host, Module, DateTimeOffset.UtcNow, out _);

        room.RejectRematch(Role.Challenger);

        room.Status.Should().Be(RoomStatus.Closed);
        room.RematchOffererRole.Should().BeNull();
    }

    [Fact]
    public void RejectRematch_by_the_offerer_throws()
    {
        var room = EndedRoom();
        room.TryOfferRematch(Role.Host, Module, DateTimeOffset.UtcNow, out _);

        var act = () => room.RejectRematch(Role.Host);

        act.Should().Throw<DomainException>();
        room.Status.Should().Be(RoomStatus.AwaitingRematch);
    }

    [Fact]
    public void Sides_alternate_across_three_consecutive_rematches()
    {
        // §1 #15 deterministic swap on every accept. Pin the alternation
        // across multiple rematches so a future "alternate only on
        // odd-numbered" implementation drift is caught.
        var room = EndedRoom();
        room.Host.Side.Should().Be(TicTacToeSides.X);

        for (var i = 0; i < 3; i++)
        {
            room.TryOfferRematch(Role.Host, Module, DateTimeOffset.UtcNow, out _);
            room.AcceptRematch(Role.Challenger, Module, DateTimeOffset.UtcNow);

            // Walk the new match to a fresh Ended state for the next loop.
            room.CurrentMatch!.Resign(room.Host.Side!);
            room.EndCurrentMatch();
        }

        // Started with host=X; after three swaps host should be O (X→O→X→O).
        room.Host.Side.Should().Be(TicTacToeSides.O);
        room.Challenger!.Side.Should().Be(TicTacToeSides.X);
    }

    [Fact]
    public void SeriesScore_carries_across_a_rematch()
    {
        // Host wins the first match (challenger resigns).
        var room = RoomFactory.InProgress(DateTimeOffset.UtcNow, Budget);
        room.CurrentMatch!.Resign(TicTacToeSides.O);
        room.EndCurrentMatch();
        room.SeriesScore.Host.Should().Be(1);

        room.TryOfferRematch(Role.Host, Module, DateTimeOffset.UtcNow, out _);
        room.AcceptRematch(Role.Challenger, Module, DateTimeOffset.UtcNow);

        room.SeriesScore.Should().Be(new SeriesScore(Host: 1, Challenger: 0, Draws: 0));
        room.CurrentMatch.Should().NotBeNull();
        room.CurrentMatch!.Outcome.Should().BeNull();
    }

    private static Room EndedRoom()
    {
        var room = RoomFactory.InProgress(DateTimeOffset.UtcNow, Budget);
        room.CurrentMatch!.Resign(TicTacToeSides.X);
        room.EndCurrentMatch();
        return room;
    }
}
