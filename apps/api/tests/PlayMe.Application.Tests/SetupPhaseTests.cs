using System.Text.Json;
using FluentAssertions;
using PlayMe.Application.Commands.AcceptRematch;
using PlayMe.Application.Commands.AdjudicateSetupTimeout;
using PlayMe.Application.Commands.OfferRematch;
using PlayMe.Application.Commands.RegisterPresence;
using PlayMe.Application.Commands.ReleasePresence;
using PlayMe.Application.Commands.SubmitSetup;
using PlayMe.Application.Dtos;
using PlayMe.Application.Errors;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Domain.Platform;
using PlayMe.Infrastructure.Security;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Seam C (Sprint 10): the module-declared setup phase. Contract under
/// test — per docs/roadmap/sprint-10-sea-battle.md: setup games enter
/// SettingUp (unclocked, bounded by the module's SetupBudget) instead of
/// InProgress; one final commit per side; both-committed starts the clock;
/// the deadline forfeits an uncommitted side (or expires the room when
/// neither committed); setup-phase presence is tracked like in-match
/// presence; rematches re-enter setup with fresh state. Setup-less games
/// are covered by the existing suites — their flow must not change.
/// </summary>
public sealed class SetupPhaseTests
{
    private static MoveDto AnyPayload() =>
        new(JsonSerializer.SerializeToElement(new { }));

    private sealed class Fixture
    {
        public FakeClock Clock { get; } = new();
        public FakeRoomRepository Rooms { get; } = new();
        public RecordingTimeoutScheduler Timeouts { get; } = new();
        public RecordingSetupDeadlineScheduler SetupDeadlines { get; } = new();
        public RecordingGraceScheduler Graces { get; } = new();
        public RecordingPostMatchExitGraceScheduler PostMatchGraces { get; } = new();
        public RecordingRoomExpiryScheduler Expiry { get; } = new();
        public RecordingAnalyticsClient Analytics { get; } = new();
        public FakeSetupGameModule Module { get; } = new();
        public StubModuleRegistry Registry { get; }

        public Fixture()
        {
            Registry = new StubModuleRegistry(Module, new FakeSetupMoveParser());
        }

        /// <summary>Room with both players registered + connected, in SettingUp.</summary>
        public Room SeedSettingUpRoom()
        {
            var room = Room.Create(
                new RoomCode(RoomFactory.RoomCodeValue),
                Module.Id,
                SideSelectionMode.HostPicksSpecific,
                new Player(
                    new PlayerId(RoomFactory.HostPlayerId),
                    DisplayName.Create("Host"),
                    "first"),
                Clock.UtcNow,
                gameOptions: null);
            room.RegisterChallenger(
                new Player(
                    new PlayerId(RoomFactory.ChallengerPlayerId),
                    DisplayName.Create("Challenger"),
                    Side: null),
                challengerPickedSide: null,
                Module);
            room.MarkConnected(Role.Host);
            room.MarkConnected(Role.Challenger);
            room.TryStartMatch(Module, Module.DefaultClockBudget, Clock.UtcNow);
            Rooms.Seed(room);
            return room;
        }

        public SubmitSetupHandler SubmitSetup() =>
            new(Rooms, Registry, Clock, Timeouts, SetupDeadlines, new RecordingRateLimiter());

        public RegisterPresenceHandler RegisterPresence() =>
            new(Rooms, Registry, Clock, Timeouts, Graces, PostMatchGraces, Expiry, SetupDeadlines);

        public ReleasePresenceHandler ReleasePresence() =>
            new(Rooms, Clock, Graces, PostMatchGraces, Registry);

        public AdjudicateSetupTimeoutHandler AdjudicateSetupTimeout() =>
            new(Rooms, Registry, Clock, new RoomCodeRedactor(), Analytics,
                Microsoft.Extensions.Logging.Abstractions
                    .NullLogger<AdjudicateSetupTimeoutHandler>.Instance);

        public Task<Room?> LoadRoom() =>
            Rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);

        public Task<AppResult<SubmitSetupResult>> Commit(Role role) =>
            SubmitSetup().HandleAsync(
                new SubmitSetupCommand(
                    RoomFactory.RoomCodeValue,
                    role == Role.Host ? RoomFactory.HostPlayerId : RoomFactory.ChallengerPlayerId,
                    role,
                    AnyPayload()),
                CancellationToken.None);
    }

    // --- Entering the setup phase ---------------------------------------

    [Fact]
    public async Task Both_players_present_puts_a_setup_game_into_SettingUp_not_InProgress()
    {
        var f = new Fixture();
        var room = Room.Create(
            new RoomCode(RoomFactory.RoomCodeValue),
            f.Module.Id,
            SideSelectionMode.HostPicksSpecific,
            new Player(new PlayerId(RoomFactory.HostPlayerId), DisplayName.Create("Host"), "first"),
            f.Clock.UtcNow,
            gameOptions: null);
        room.RegisterChallenger(
            new Player(
                new PlayerId(RoomFactory.ChallengerPlayerId),
                DisplayName.Create("Challenger"),
                Side: null),
            challengerPickedSide: null,
            f.Module);
        room.MarkConnected(Role.Host);
        f.Rooms.Seed(room);

        var result = await f.RegisterPresence().HandleAsync(
            new RegisterPresenceCommand(
                RoomFactory.RoomCodeValue, RoomFactory.ChallengerPlayerId, Role.Challenger),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.MatchJustStarted.Should().BeTrue();
        result.Value.Room.Status.Should().Be(RoomStatus.SettingUp);
        result.Value.Room.CurrentMatch!.Setup.Should().Be(
            new SetupStateDto(HostCommitted: false, ChallengerCommitted: false));

        // The setup deadline is enrolled at now + SetupBudget; the chess
        // clock is NOT scheduled (setup is unclocked); the unjoined-room
        // expiry entry is cancelled.
        f.SetupDeadlines.Scheduled.Should().ContainSingle()
            .Which.Deadline.Should().Be(f.Clock.UtcNow + f.Module.SetupBudget);
        f.Timeouts.Scheduled.Should().BeEmpty();
        f.Expiry.Cancelled.Should().ContainSingle();
    }

    // --- Committing -------------------------------------------------------

    [Fact]
    public async Task First_commit_records_readiness_without_starting_the_match()
    {
        var f = new Fixture();
        f.SeedSettingUpRoom();

        var result = await f.Commit(Role.Host);

        result.Succeeded.Should().BeTrue();
        result.Value!.MatchStarted.Should().BeFalse();
        result.Value.Room.Status.Should().Be(RoomStatus.SettingUp);
        result.Value.Room.CurrentMatch!.Setup.Should().Be(
            new SetupStateDto(HostCommitted: true, ChallengerCommitted: false));
        f.Timeouts.Scheduled.Should().BeEmpty();
        f.SetupDeadlines.Cancelled.Should().BeEmpty();
    }

    [Fact]
    public async Task Second_commit_completes_setup_and_starts_the_clock_from_completion_time()
    {
        var f = new Fixture();
        f.SeedSettingUpRoom();

        await f.Commit(Role.Host);
        f.Clock.Advance(TimeSpan.FromSeconds(42));
        var result = await f.Commit(Role.Challenger);

        result.Succeeded.Should().BeTrue();
        result.Value!.MatchStarted.Should().BeTrue();
        result.Value.Room.Status.Should().Be(RoomStatus.InProgress);

        var saved = await f.LoadRoom();
        saved!.Status.Should().Be(RoomStatus.InProgress);
        // The 42 unclocked setup seconds are discarded — the first mover
        // starts from the full budget at the completion moment.
        saved.CurrentMatch!.Clock.LastTickAt.Should().Be(f.Clock.UtcNow);
        saved.CurrentMatch.Clock.HostRemaining.Should().Be(f.Module.DefaultClockBudget);
        saved.CurrentMatch.Clock.ChallengerRemaining.Should().Be(f.Module.DefaultClockBudget);

        f.SetupDeadlines.Cancelled.Should().ContainSingle();
        f.Timeouts.Scheduled.Should().ContainSingle()
            .Which.Deadline.Should().Be(f.Clock.UtcNow + f.Module.DefaultClockBudget);
    }

    [Fact]
    public async Task Double_commit_is_rejected_with_the_platform_key()
    {
        var f = new Fixture();
        f.SeedSettingUpRoom();

        await f.Commit(Role.Host);
        var second = await f.Commit(Role.Host);

        second.Succeeded.Should().BeFalse();
        second.Error.Should().Be(PlatformErrors.SetupAlreadyCommitted);
    }

    [Fact]
    public async Task Module_validation_failure_passes_the_reject_key_through()
    {
        var f = new Fixture();
        f.SeedSettingUpRoom();
        f.Module.NextRejectKey = "fakesetup.invalidFleet";

        var result = await f.Commit(Role.Host);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("fakesetup.invalidFleet");

        // A rejected commit leaves the player uncommitted.
        var saved = await f.LoadRoom();
        saved!.CurrentMatch!.HostSetupCommitted.Should().BeFalse();
    }

    [Fact]
    public async Task Commit_outside_SettingUp_is_rejected()
    {
        var f = new Fixture();
        f.SeedSettingUpRoom();
        await f.Commit(Role.Host);
        await f.Commit(Role.Challenger); // setup complete → InProgress

        var late = await f.Commit(Role.Host);

        late.Succeeded.Should().BeFalse();
        late.Error.Should().Be(PlatformErrors.SetupNotInSetup);
    }

    // --- Setup deadline ----------------------------------------------------

    [Fact]
    public async Task Deadline_with_one_uncommitted_side_expires_the_room_without_a_loss()
    {
        var f = new Fixture();
        f.SeedSettingUpRoom();
        await f.Commit(Role.Host);

        f.Clock.Advance(f.Module.SetupBudget + TimeSpan.FromSeconds(1));
        var result = await f.AdjudicateSetupTimeout().HandleAsync(
            new AdjudicateSetupTimeoutCommand(RoomFactory.RoomCodeValue),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Expired.Should().BeTrue();

        // No forfeit: setup expiry never awards a win — the match never
        // started, so there's no outcome and the scoreboard is untouched.
        var saved = await f.LoadRoom();
        saved!.Status.Should().Be(RoomStatus.Expired);
        saved.CurrentMatch!.Outcome.Should().BeNull();
        saved.SeriesScore.Host.Should().Be(0);
        saved.SeriesScore.Challenger.Should().Be(0);
        f.Analytics.Events.Should().ContainSingle(e => e.Event == "room_expired");
    }

    [Fact]
    public async Task Deadline_with_neither_side_committed_expires_the_room()
    {
        var f = new Fixture();
        f.SeedSettingUpRoom();

        f.Clock.Advance(f.Module.SetupBudget + TimeSpan.FromSeconds(1));
        var result = await f.AdjudicateSetupTimeout().HandleAsync(
            new AdjudicateSetupTimeoutCommand(RoomFactory.RoomCodeValue),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Expired.Should().BeTrue();

        var saved = await f.LoadRoom();
        saved!.Status.Should().Be(RoomStatus.Expired);
        saved.CurrentMatch!.Outcome.Should().BeNull();
        f.Analytics.Events.Should().ContainSingle(e => e.Event == "room_expired");
    }

    [Fact]
    public async Task Stale_deadline_after_setup_completed_is_dropped()
    {
        var f = new Fixture();
        f.SeedSettingUpRoom();
        await f.Commit(Role.Host);
        await f.Commit(Role.Challenger); // InProgress

        var result = await f.AdjudicateSetupTimeout().HandleAsync(
            new AdjudicateSetupTimeoutCommand(RoomFactory.RoomCodeValue),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Expired.Should().BeFalse();
        (await f.LoadRoom())!.Status.Should().Be(RoomStatus.InProgress);
    }

    // --- Presence during setup ----------------------------------------------

    [Fact]
    public async Task Disconnecting_during_setup_notifies_opponent_but_schedules_no_grace()
    {
        var f = new Fixture();
        f.SeedSettingUpRoom();

        var result = await f.ReleasePresence().HandleAsync(
            new ReleasePresenceCommand(
                RoomFactory.RoomCodeValue, RoomFactory.ChallengerPlayerId, Role.Challenger),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Effect.Should().Be(PresenceReleaseEffect.OpponentDisconnected);
        // No grace during setup: the setup deadline is the only
        // adjudicating authority there, and it never awards a win — a
        // grace would race it and end the match with a Disconnect loss
        // mid-handshake.
        f.Graces.Scheduled.Should().BeEmpty();
    }

    // --- Rematch re-enters setup ---------------------------------------------

    [Fact]
    public async Task Accepted_rematch_re_enters_setup_with_fresh_state_and_swapped_sides()
    {
        var f = new Fixture();
        f.SeedSettingUpRoom();
        await f.Commit(Role.Host);
        await f.Commit(Role.Challenger); // setup complete → InProgress
        // End the first match so the rematch flow is reachable (setup
        // expiry is terminal and never ends a match — resign is the
        // shortest route to Ended from here).
        var firstMatchRoom = await f.LoadRoom();
        firstMatchRoom!.CurrentMatch!.Resign("second");
        firstMatchRoom.EndCurrentMatch();
        await f.Rooms.SaveAsync(firstMatchRoom, CancellationToken.None);

        var offer = new OfferRematchHandler(
            f.Rooms, f.Registry, f.Clock, f.Timeouts, f.SetupDeadlines, new RecordingRateLimiter());
        var accept = new AcceptRematchHandler(
            f.Rooms, f.Registry, f.Clock, f.Timeouts, f.SetupDeadlines, new RecordingRateLimiter());

        (await offer.HandleAsync(
            new OfferRematchCommand(
                RoomFactory.RoomCodeValue, RoomFactory.HostPlayerId, Role.Host),
            CancellationToken.None)).Succeeded.Should().BeTrue();
        var accepted = await accept.HandleAsync(
            new AcceptRematchCommand(
                RoomFactory.RoomCodeValue, RoomFactory.ChallengerPlayerId, Role.Challenger),
            CancellationToken.None);

        accepted.Succeeded.Should().BeTrue();
        accepted.Value!.Room.Status.Should().Be(RoomStatus.SettingUp);

        var saved = await f.LoadRoom();
        saved!.Status.Should().Be(RoomStatus.SettingUp);
        // Fresh setup state — nobody is committed in the new match.
        saved.CurrentMatch!.HostSetupCommitted.Should().BeFalse();
        saved.CurrentMatch.ChallengerSetupCommitted.Should().BeFalse();
        // Sides swapped per platform.md §1 #15 — first-shot advantage alternates.
        saved.Host.Side.Should().Be("second");
        saved.Challenger!.Side.Should().Be("first");
        // A fresh setup deadline was scheduled (the fixture seeds the
        // initial SettingUp room directly, so this is the recorder's only
        // entry). The single clock-timeout entry is the FIRST match's,
        // from its setup completing — re-entering SettingUp schedules a
        // setup deadline, not a chess-clock timeout.
        f.SetupDeadlines.Scheduled.Should().ContainSingle()
            .Which.Deadline.Should().Be(f.Clock.UtcNow + f.Module.SetupBudget);
        f.Timeouts.Scheduled.Should().ContainSingle();
    }
}
