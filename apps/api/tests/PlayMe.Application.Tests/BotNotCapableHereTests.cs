using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PlayMe.Application.Commands.PlayBotMove;
using PlayMe.Application.Commands.PlayBotSetup;
using PlayMe.Application.Errors;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Application.Time;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Rolling-deploy hand-off (Sprint 12, found in PR6): a bot room whose
/// game has no bot in <em>this</em> instance's build must not be treated
/// as a module fault. The handlers report <see cref="PlatformErrors.BotNotCapableHere"/>
/// and leave the room untouched, so the sweeper keeps the entry for an
/// instance that can serve it.
/// </summary>
public sealed class BotNotCapableHereTests
{
    [Fact]
    public async Task Move_handler_hands_off_when_the_module_has_no_bot_here()
    {
        // The room was created elsewhere with a bot seat; this instance's
        // module for the same game id lacks IBotPlayer.
        var created = new FakeBotGameModule();
        var hereModule = new FakeKeepTurnModule();
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var room = Room.CreateWithBot(
            new RoomCode(RoomFactory.RoomCodeValue), hereModule.Id, SideSelectionMode.HostPicksSpecific,
            BotRoomFactory.Host("second"), BotRoomFactory.Bot(), BotLevel.Easy, created, clock.UtcNow);
        room.MarkConnected(Role.Host);
        room.TryStartMatch(hereModule, TimeSpan.FromMinutes(1), clock.UtcNow); // bot ("first") to move
        rooms.Seed(room);
        var timeouts = new RecordingTimeoutScheduler();

        var handler = new PlayBotMoveHandler(
            rooms,
            new StubModuleRegistry(hereModule, new FakeKeepTurnMoveParser()),
            clock,
            new ClockService(),
            new SeededRandom(),
            TestHandlers.MoveCommitter(rooms, timeouts),
            NullLogger<PlayBotMoveHandler>.Instance);

        var result = await handler.HandleAsync(new PlayBotMoveCommand(RoomFactory.RoomCodeValue), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(PlatformErrors.BotNotCapableHere);
        var after = (await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), CancellationToken.None))!;
        after.CurrentMatch!.MoveCount.Should().Be(0);
        after.CurrentMatch.Outcome.Should().BeNull("a hand-off never forfeits");
        timeouts.Cancelled.Should().BeEmpty();
    }

    [Fact]
    public async Task Setup_handler_hands_off_when_the_module_has_no_bot_setup_here()
    {
        var created = new FakeBotSetupGameModule();
        var hereModule = new FakeSetupGameModule(); // ISetupGame, not IBotSetup
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var room = Room.CreateWithBot(
            new RoomCode(RoomFactory.RoomCodeValue), hereModule.Id, SideSelectionMode.HostPicksSpecific,
            BotRoomFactory.Host("first"), BotRoomFactory.Bot(), BotLevel.Easy, created, clock.UtcNow);
        room.MarkConnected(Role.Host);
        room.TryStartMatch(hereModule, TimeSpan.FromMinutes(1), clock.UtcNow); // SettingUp
        rooms.Seed(room);

        var handler = new PlayBotSetupHandler(
            rooms,
            new StubModuleRegistry(hereModule),
            clock,
            new SeededRandom(),
            TestHandlers.SetupCommitter(rooms, new RecordingTimeoutScheduler()),
            NullLogger<PlayBotSetupHandler>.Instance);

        var result = await handler.HandleAsync(new PlayBotSetupCommand(RoomFactory.RoomCodeValue), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(PlatformErrors.BotNotCapableHere);
        var after = (await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), CancellationToken.None))!;
        after.Status.Should().Be(RoomStatus.SettingUp);
        after.CurrentMatch!.ChallengerSetupCommitted.Should().BeFalse();
    }
}
