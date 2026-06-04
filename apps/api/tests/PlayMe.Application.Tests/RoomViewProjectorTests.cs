using FluentAssertions;
using PlayMe.Application.Dtos;
using PlayMe.Application.Mapping;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Seam A (Sprint 10): wire-boundary projection for hidden-state games.
/// The contract under test — per docs/roadmap/sprint-10-sea-battle.md:
/// live hidden-state matches project per viewer (null viewer = the
/// module's public view); perfect-information games, matchless rooms,
/// and terminal matches pass through as the same instance.
/// </summary>
public sealed class RoomViewProjectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    private static RoomDto Room(
        GameId gameId,
        MatchDto? match,
        string hostSide = "first",
        string? challengerSide = "second") => new(
        Code: new RoomCode("ABCDEF"),
        GameId: gameId,
        SideSelectionMode: SideSelectionMode.HostPicksSpecific,
        Status: match is null ? RoomStatus.WaitingForOpponent : RoomStatus.InProgress,
        Host: new PlayerDto("Host", hostSide),
        Challenger: new PlayerDto("Challenger", challengerSide),
        HostConnected: true,
        ChallengerConnected: true,
        CurrentMatch: match,
        CreatedAt: Now,
        Score: new ScoreDto(0, 0, 0),
        RematchOffererRole: null);

    private static MatchDto Match(GameId gameId, string state, OutcomeDto? outcome = null) => new(
        GameId: gameId,
        SideToMove: "first",
        MoveCount: 3,
        State: state,
        Clock: new ClockSnapshotDto(60_000, 60_000, "host", Now, Now),
        Outcome: outcome);

    private static readonly FakeHiddenStateModule Hidden = new();
    private static readonly StubModuleRegistry HiddenRegistry = new(Hidden);

    [Fact]
    public void Hidden_live_match_projects_per_role()
    {
        var room = Room(Hidden.Id, Match(Hidden.Id, "full"));

        var hostView = RoomViewProjector.ForViewer(room, Role.Host, HiddenRegistry);
        var challengerView = RoomViewProjector.ForViewer(room, Role.Challenger, HiddenRegistry);

        hostView.CurrentMatch!.State.Should().Be("full:view-first");
        challengerView.CurrentMatch!.State.Should().Be("full:view-second");
    }

    [Fact]
    public void Hidden_live_match_projects_anonymous_viewer_to_public_view()
    {
        var room = Room(Hidden.Id, Match(Hidden.Id, "full"));

        var anonymousView = RoomViewProjector.ForViewer(room, viewer: null, HiddenRegistry);

        anonymousView.CurrentMatch!.State.Should().Be("full:public");
    }

    [Fact]
    public void Hidden_live_match_projects_only_the_state_field()
    {
        var room = Room(Hidden.Id, Match(Hidden.Id, "full"));

        var hostView = RoomViewProjector.ForViewer(room, Role.Host, HiddenRegistry);

        hostView.Should().BeEquivalentTo(
            room, options => options.Excluding(r => r.CurrentMatch!.State));
    }

    [Fact]
    public void Hidden_terminal_match_returns_full_state_to_both()
    {
        var ended = Match(Hidden.Id, "full",
            new OutcomeDto("win", WinningSide: "first", null, null));
        var room = Room(Hidden.Id, ended);

        RoomViewProjector.ForViewer(room, Role.Host, HiddenRegistry)
            .Should().BeSameAs(room);
        RoomViewProjector.ForViewer(room, Role.Challenger, HiddenRegistry)
            .Should().BeSameAs(room);
        RoomViewProjector.ForViewer(room, viewer: null, HiddenRegistry)
            .Should().BeSameAs(room);
    }

    [Fact]
    public void Hidden_room_without_match_passes_through()
    {
        var room = Room(Hidden.Id, match: null);

        RoomViewProjector.ForViewer(room, Role.Host, HiddenRegistry)
            .Should().BeSameAs(room);
    }

    [Fact]
    public void Perfect_information_module_passes_through_as_same_instance()
    {
        var registry = new SingleGameRegistry();
        var gameId = registry.GetModule(new GameId("tictactoe")).Id;
        var room = Room(gameId, Match(gameId, "........."), hostSide: "x", challengerSide: "o");

        RoomViewProjector.ForViewer(room, Role.Host, registry)
            .Should().BeSameAs(room);
        RoomViewProjector.ForViewer(room, viewer: null, registry)
            .Should().BeSameAs(room);
    }

    [Fact]
    public void RequiresProjection_is_true_only_for_live_hidden_state_matches()
    {
        var liveHidden = Room(Hidden.Id, Match(Hidden.Id, "full"));
        var endedHidden = Room(Hidden.Id, Match(Hidden.Id, "full",
            new OutcomeDto("draw", null, null, null)));
        var matchless = Room(Hidden.Id, match: null);

        RoomViewProjector.RequiresProjection(liveHidden, HiddenRegistry).Should().BeTrue();
        RoomViewProjector.RequiresProjection(endedHidden, HiddenRegistry).Should().BeFalse();
        RoomViewProjector.RequiresProjection(matchless, HiddenRegistry).Should().BeFalse();

        var registry = new SingleGameRegistry();
        var gameId = registry.GetModule(new GameId("tictactoe")).Id;
        var perfectInfo = Room(gameId, Match(gameId, "........."), hostSide: "x", challengerSide: "o");
        RoomViewProjector.RequiresProjection(perfectInfo, registry).Should().BeFalse();
    }
}
