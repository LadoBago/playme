using FluentAssertions;
using PlayMe.Application.Commands.RegisterPresence;
using PlayMe.Application.Commands.ReleasePresence;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Domain.Games.TicTacToe;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Presence handler clock / reconnect behaviour added in Sprint 2:
/// initialising the clock at match start, scheduling / cancelling the
/// disconnect-grace entry on release/register, and reporting reconnect
/// so the Hub can emit <c>OpponentReconnected</c>.
/// </summary>
public sealed class PresenceHandlerTests
{
    // Explicit budget handed to RoomFactory.InProgress fixtures. Kept above
    // the 1-min tier so the in-progress disconnect grace (60s) still applies —
    // these tests exercise grace/reconnect, not the module's size-derived budget.
    private static readonly TimeSpan Budget = TimeSpan.FromMinutes(3);

    // What TryStartMatch installs for the default 3×3 fixture: tictactoe
    // derives its clock budget from boardSize (3×3 → 1 min). See
    // TicTacToeGameModule.ClockBudgetFor.
    private static readonly TimeSpan StartBudget = TimeSpan.FromMinutes(1);

    [Fact]
    public async Task RegisterPresence_starts_match_initialises_clock_and_schedules_first_timeout()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        var graces = new RecordingGraceScheduler();
        var postMatchGraces = new RecordingPostMatchExitGraceScheduler();
        var expiry = new RecordingRoomExpiryScheduler();

        // Seed a fresh room awaiting both players' connection. The unified
        // tictactoe module requires gameOptions; the test triggers
        // TryStartMatch via the handler so omitting them would throw at
        // module.NewMatch time.
        var seed = Room.Create(
            new RoomCode(RoomFactory.RoomCodeValue),
            TicTacToeGameModule.GameId,
            SideSelectionMode.HostPicksSpecific,
            new Player(
                new PlayerId(RoomFactory.HostPlayerId),
                DisplayName.Create("Host"),
                TicTacToeSides.X),
            clock.UtcNow,
            gameOptions: RoomFactory.DefaultGameOptions());
        seed.RegisterChallenger(
            new Player(
                new PlayerId(RoomFactory.ChallengerPlayerId),
                DisplayName.Create("Challenger"),
                Side: null),
            challengerPickedSide: null,
            new TicTacToeGameModule());
        rooms.Seed(seed);

        var handler = new RegisterPresenceHandler(
            rooms, new SingleGameRegistry(), clock, timeouts, graces, postMatchGraces, expiry, new RecordingSetupDeadlineScheduler());

        // Host connects — match doesn't start yet (challenger still offline).
        var hostResult = await handler.HandleAsync(
            new RegisterPresenceCommand(
                RoomFactory.RoomCodeValue, RoomFactory.HostPlayerId, Role.Host),
            CancellationToken.None);
        hostResult.Value!.MatchJustStarted.Should().BeFalse();
        timeouts.Scheduled.Should().BeEmpty();

        // Challenger connects — match starts now.
        var challengerResult = await handler.HandleAsync(
            new RegisterPresenceCommand(
                RoomFactory.RoomCodeValue, RoomFactory.ChallengerPlayerId, Role.Challenger),
            CancellationToken.None);
        challengerResult.Value!.MatchJustStarted.Should().BeTrue();
        challengerResult.Value.Reconnected.Should().BeFalse();

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.CurrentMatch!.Clock.HostRemaining.Should().Be(StartBudget);
        saved.CurrentMatch.Clock.ChallengerRemaining.Should().Be(StartBudget);
        saved.CurrentMatch.Clock.ActivePlayer.Should().Be(Role.Host);

        timeouts.Scheduled.Should().HaveCount(1);
        timeouts.Scheduled[0].Deadline.Should().Be(clock.UtcNow + StartBudget);

        // The WaitingForOpponent → InProgress transition cancels the
        // unjoined-expiry entry so the sweeper doesn't fire room_expired
        // for a room that actually made it to gameplay.
        expiry.Cancelled.Should().ContainSingle()
            .Which.Should().Be((RoomFactory.RoomCodeValue, TicTacToeGameModule.GameId.Value));
    }

    // OneMin-tier coverage (no grace for budgets ≤ 1 min, per
    // docs/platform.md §1 #7) lives in GraceSchedulingPolicyTests, which
    // exercises the tier rule directly across all budgets. The unified
    // tictactoe module does return 1 min for the 3×3 board, but these
    // handler tests pin the start-clock / reconnect plumbing rather than
    // re-deriving the grace tiers, so they keep an explicit >1-min Budget.


    [Fact]
    public async Task ReleasePresence_from_Ended_schedules_post_match_exit_grace()
    {
        // Post-match disconnect (state.md §2.4): refresh, locale toggle,
        // and transient blips all manifest as a SignalR drop here. The
        // handler must NOT immediately close the room or notify the
        // opponent; instead it schedules a brief reconnect grace and
        // returns Effect.None. The sweeper closes the room + emits
        // OpponentExited only if the grace elapses without a reconnect.
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var graces = new RecordingGraceScheduler();
        var postMatchGraces = new RecordingPostMatchExitGraceScheduler();
        var seed = RoomFactory.InProgress(clock.UtcNow, Budget);
        seed.CurrentMatch!.Resign(TicTacToeSides.X);
        seed.EndCurrentMatch();
        rooms.Seed(seed);

        var handler = new ReleasePresenceHandler(
            rooms, clock, graces, postMatchGraces, new SingleGameRegistry());

        var result = await handler.HandleAsync(
            new ReleasePresenceCommand(
                RoomFactory.RoomCodeValue,
                RoomFactory.HostPlayerId,
                Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Effect.Should().Be(PresenceReleaseEffect.None);

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.Status.Should().Be(RoomStatus.Ended);
        saved.HostConnected.Should().BeFalse();
        graces.Scheduled.Should().BeEmpty();

        postMatchGraces.Scheduled.Should().ContainSingle()
            .Which.Should().Be((
                RoomFactory.RoomCodeValue,
                Role.Host,
                clock.UtcNow + ReleasePresenceHandler.PostMatchExitGracePeriod));
    }

    [Fact]
    public async Task ReleasePresence_when_caller_already_disconnected_is_silent()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var graces = new RecordingGraceScheduler();
        var postMatchGraces = new RecordingPostMatchExitGraceScheduler();
        var seed = RoomFactory.InProgress(clock.UtcNow, Budget);
        // Pre-condition: host has already dropped (e.g. earlier disconnect).
        seed.MarkDisconnected(Role.Host);
        rooms.Seed(seed);

        var handler = new ReleasePresenceHandler(
            rooms, clock, graces, postMatchGraces, new SingleGameRegistry());

        // A second disconnect — e.g. a stale-cookie probe that briefly
        // connected and tore down — must NOT re-broadcast OpponentDisconnected
        // or schedule another grace entry.
        var result = await handler.HandleAsync(
            new ReleasePresenceCommand(
                RoomFactory.RoomCodeValue,
                RoomFactory.HostPlayerId,
                Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Effect.Should().Be(PresenceReleaseEffect.None);
        graces.Scheduled.Should().BeEmpty();
        postMatchGraces.Scheduled.Should().BeEmpty();
    }

    [Fact]
    public async Task RegisterPresence_on_reconnect_cancels_grace_and_flags_reconnected()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        var graces = new RecordingGraceScheduler();
        var postMatchGraces = new RecordingPostMatchExitGraceScheduler();
        rooms.Seed(RoomFactory.InProgress(clock.UtcNow, Budget));

        // Drop the host.
        var release = new ReleasePresenceHandler(
            rooms, clock, graces, postMatchGraces, new SingleGameRegistry());
        await release.HandleAsync(
            new ReleasePresenceCommand(
                RoomFactory.RoomCodeValue, RoomFactory.HostPlayerId, Role.Host),
            CancellationToken.None);

        // Re-connect 5s later.
        clock.Advance(TimeSpan.FromSeconds(5));
        var register = new RegisterPresenceHandler(
            rooms,
            new SingleGameRegistry(),
            clock,
            timeouts,
            graces,
            postMatchGraces,
            new RecordingRoomExpiryScheduler(),
            new RecordingSetupDeadlineScheduler());
        var result = await register.HandleAsync(
            new RegisterPresenceCommand(
                RoomFactory.RoomCodeValue, RoomFactory.HostPlayerId, Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.MatchJustStarted.Should().BeFalse();
        result.Value.Reconnected.Should().BeTrue();

        graces.Cancelled.Should().Contain((RoomFactory.RoomCodeValue, Role.Host));

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.HostConnected.Should().BeTrue();
        // Clock kept ticking through the disconnect — state.md §2.4 invariant.
        saved.CurrentMatch!.Clock.LastTickAt.Should().Be(clock.UtcNow - TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RegisterPresence_on_post_match_reconnect_cancels_post_match_grace()
    {
        // State.md §2.4: a reconnect during the post-match exit grace
        // window must cancel the pending entry so the sweeper doesn't
        // close the room and broadcast OpponentExited. Mirrors the
        // in-progress reconnect path but on the post-match scheduler.
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        var graces = new RecordingGraceScheduler();
        var postMatchGraces = new RecordingPostMatchExitGraceScheduler();
        var seed = RoomFactory.InProgress(clock.UtcNow, Budget);
        seed.CurrentMatch!.Resign(TicTacToeSides.X);
        seed.EndCurrentMatch();
        rooms.Seed(seed);

        // Drop the host from Ended.
        var release = new ReleasePresenceHandler(
            rooms, clock, graces, postMatchGraces, new SingleGameRegistry());
        await release.HandleAsync(
            new ReleasePresenceCommand(
                RoomFactory.RoomCodeValue, RoomFactory.HostPlayerId, Role.Host),
            CancellationToken.None);
        postMatchGraces.Scheduled.Should().ContainSingle();

        // Re-connect within the grace window.
        clock.Advance(TimeSpan.FromSeconds(3));
        var register = new RegisterPresenceHandler(
            rooms,
            new SingleGameRegistry(),
            clock,
            timeouts,
            graces,
            postMatchGraces,
            new RecordingRoomExpiryScheduler(),
            new RecordingSetupDeadlineScheduler());
        var result = await register.HandleAsync(
            new RegisterPresenceCommand(
                RoomFactory.RoomCodeValue, RoomFactory.HostPlayerId, Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        postMatchGraces.Cancelled.Should().Contain((RoomFactory.RoomCodeValue, Role.Host));

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.Status.Should().Be(RoomStatus.Ended);
        saved.HostConnected.Should().BeTrue();
    }
}
