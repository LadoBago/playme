using FluentAssertions;
using PlayMe.Application.Commands.RegisterPresence;
using PlayMe.Application.Commands.ReleasePresence;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Domain.Games.TicTacToe3x3;
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
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task RegisterPresence_starts_match_initialises_clock_and_schedules_first_timeout()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        var graces = new RecordingGraceScheduler();
        var postMatchGraces = new RecordingPostMatchExitGraceScheduler();
        var expiry = new RecordingRoomExpiryScheduler();

        // Seed a fresh room awaiting both players' connection.
        var seed = Room.Create(
            new RoomCode(RoomFactory.RoomCodeValue),
            TicTacToe3x3GameModule.GameId,
            SideSelectionMode.HostPicksSpecific,
            new Player(
                new PlayerId(RoomFactory.HostPlayerId),
                DisplayName.Create("Host"),
                TicTacToeSides.X),
            clock.UtcNow);
        seed.RegisterChallenger(
            new Player(
                new PlayerId(RoomFactory.ChallengerPlayerId),
                DisplayName.Create("Challenger"),
                Side: null),
            challengerPickedSide: null,
            new TicTacToe3x3GameModule());
        rooms.Seed(seed);

        var handler = new RegisterPresenceHandler(
            rooms, new SingleGameRegistry(), clock, timeouts, graces, postMatchGraces, expiry);

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
        saved!.CurrentMatch!.Clock.HostRemaining.Should().Be(Budget);
        saved.CurrentMatch.Clock.ChallengerRemaining.Should().Be(Budget);
        saved.CurrentMatch.Clock.ActivePlayer.Should().Be(Role.Host);

        timeouts.Scheduled.Should().HaveCount(1);
        timeouts.Scheduled[0].Deadline.Should().Be(clock.UtcNow + Budget);

        // The WaitingForOpponent → InProgress transition cancels the
        // unjoined-expiry entry so the sweeper doesn't fire room_expired
        // for a room that actually made it to gameplay.
        expiry.Cancelled.Should().ContainSingle()
            .Which.Should().Be((RoomFactory.RoomCodeValue, TicTacToe3x3GameModule.GameId.Value));
    }

    [Fact]
    public async Task ReleasePresence_during_InProgress_signals_disconnect_no_grace_for_OneMin_tier()
    {
        // TTT 3x3 has DefaultClockBudget = 60s, which sits in the
        // "≤ 1 min → no grace tier" bucket per docs/platform-and-games.md
        // §1 #7. The handler must still emit OpponentDisconnected so the
        // opponent's UI shows the transient banner — but the abandon
        // grace stays unscheduled (the chess-clock timeout catches the
        // abandon as Timeout instead).
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var graces = new RecordingGraceScheduler();
        var postMatchGraces = new RecordingPostMatchExitGraceScheduler();
        rooms.Seed(RoomFactory.InProgress(clock.UtcNow, Budget));

        var handler = new ReleasePresenceHandler(
            rooms, clock, graces, postMatchGraces, new SingleGameRegistry());

        var result = await handler.HandleAsync(
            new ReleasePresenceCommand(
                RoomFactory.RoomCodeValue,
                RoomFactory.HostPlayerId,
                Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Effect.Should().Be(PresenceReleaseEffect.OpponentDisconnected);
        graces.Scheduled.Should().BeEmpty();
        postMatchGraces.Scheduled.Should().BeEmpty();

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.HostConnected.Should().BeFalse();
    }

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
            new RecordingRoomExpiryScheduler());
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
            new RecordingRoomExpiryScheduler());
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
