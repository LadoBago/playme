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
            rooms, new SingleGameRegistry(), clock, timeouts, graces);

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
    }

    [Fact]
    public async Task ReleasePresence_during_InProgress_schedules_grace_and_signals_notification()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var graces = new RecordingGraceScheduler();
        rooms.Seed(RoomFactory.InProgress(clock.UtcNow, Budget));

        var handler = new ReleasePresenceHandler(rooms, clock, graces);

        var result = await handler.HandleAsync(
            new ReleasePresenceCommand(
                RoomFactory.RoomCodeValue,
                RoomFactory.HostPlayerId,
                Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.OpponentNotificationDue.Should().BeTrue();

        graces.Scheduled.Should().ContainSingle()
            .Which.Should().Match<(string Code, Role Role, DateTimeOffset Deadline)>(g =>
                g.Code == RoomFactory.RoomCodeValue &&
                g.Role == Role.Host &&
                g.Deadline == clock.UtcNow + PlatformConstants.DisconnectGrace);

        var saved = await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), default);
        saved!.HostConnected.Should().BeFalse();
    }

    [Fact]
    public async Task ReleasePresence_when_caller_already_disconnected_is_silent()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var graces = new RecordingGraceScheduler();
        var seed = RoomFactory.InProgress(clock.UtcNow, Budget);
        // Pre-condition: host has already dropped (e.g. earlier disconnect).
        seed.MarkDisconnected(Role.Host);
        rooms.Seed(seed);

        var handler = new ReleasePresenceHandler(rooms, clock, graces);

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
        result.Value!.OpponentNotificationDue.Should().BeFalse();
        graces.Scheduled.Should().BeEmpty();
    }

    [Fact]
    public async Task RegisterPresence_on_reconnect_cancels_grace_and_flags_reconnected()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        var graces = new RecordingGraceScheduler();
        rooms.Seed(RoomFactory.InProgress(clock.UtcNow, Budget));

        // Drop the host.
        var release = new ReleasePresenceHandler(rooms, clock, graces);
        await release.HandleAsync(
            new ReleasePresenceCommand(
                RoomFactory.RoomCodeValue, RoomFactory.HostPlayerId, Role.Host),
            CancellationToken.None);

        // Re-connect 5s later.
        clock.Advance(TimeSpan.FromSeconds(5));
        var register = new RegisterPresenceHandler(
            rooms, new SingleGameRegistry(), clock, timeouts, graces);
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
}
