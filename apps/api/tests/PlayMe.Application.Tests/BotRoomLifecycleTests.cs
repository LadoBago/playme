using FluentAssertions;
using PlayMe.Application.Commands.OfferRematch;
using PlayMe.Application.Commands.RegisterPresence;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Seam D (Sprint 12), the platform around the bot's moves: the room
/// starts the moment the host connects, the bot's presence can't be lost,
/// the bot seat is not joinable, and the bot accepts every rematch on the
/// spot with the side swap deciding whether it moves first.
/// </summary>
public sealed class BotRoomLifecycleTests
{
    private static Room Unstarted(FakeBotGameModule module, DateTimeOffset now, string hostSide) =>
        Room.CreateWithBot(
            new RoomCode(RoomFactory.RoomCodeValue),
            module.Id,
            SideSelectionMode.HostPicksSpecific,
            BotRoomFactory.Host(hostSide),
            BotRoomFactory.Bot(),
            BotLevel.Easy,
            module,
            now,
            gameOptions: null);

    [Theory]
    [InlineData(FakeBotGameModule.First, 0)]
    [InlineData(FakeBotGameModule.Second, 1)]
    public async Task Host_presence_starts_the_match_and_enrolls_the_bot_only_when_it_moves_first(
        string hostSide, int expectedBotTurns)
    {
        var module = new FakeBotGameModule();
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        var expiry = new RecordingRoomExpiryScheduler();
        var botTurns = new RecordingBotTurnScheduler();
        rooms.Seed(Unstarted(module, clock.UtcNow, hostSide));

        var handler = new RegisterPresenceHandler(
            rooms, BotRoomFactory.Registry(module), clock, timeouts, new RecordingGraceScheduler(),
            new RecordingPostMatchExitGraceScheduler(), expiry, new RecordingSetupDeadlineScheduler(),
            TestHandlers.BotTurns(botTurns));

        var result = await handler.HandleAsync(
            new RegisterPresenceCommand(RoomFactory.RoomCodeValue, RoomFactory.HostPlayerId, Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.MatchJustStarted.Should().BeTrue("the bot seat is already present and connected");
        timeouts.Scheduled.Should().ContainSingle();
        expiry.Cancelled.Should().ContainSingle();
        botTurns.Scheduled.Should().HaveCount(expectedBotTurns);
    }

    [Fact]
    public void Bot_presence_cannot_be_lost()
    {
        var module = new FakeBotGameModule();
        var room = BotRoomFactory.Started(module, new FakeClock().UtcNow);

        room.MarkDisconnected(Role.Challenger);

        room.ChallengerConnected.Should().BeTrue();
    }

    [Fact]
    public void Bot_seat_is_not_joinable()
    {
        var module = new FakeBotGameModule();
        var room = Unstarted(module, new FakeClock().UtcNow, FakeBotGameModule.First);

        var join = () => room.RegisterChallenger(
            new Player(new PlayerId("intruder"), DisplayName.Create("Intruder"), Side: null),
            challengerPickedSide: null,
            module);

        join.Should().Throw<DomainException>().WithMessage("*bot room*");
    }

    [Fact]
    public void Bot_room_refuses_challenger_picks_and_a_human_in_the_bot_seat()
    {
        var module = new FakeBotGameModule();
        var now = new FakeClock().UtcNow;

        var picks = () => Room.CreateWithBot(
            new RoomCode(RoomFactory.RoomCodeValue), module.Id, SideSelectionMode.ChallengerPicks,
            BotRoomFactory.Host(FakeBotGameModule.First) with { Side = null }, BotRoomFactory.Bot(),
            BotLevel.Easy, module, now);
        var humanBot = () => Room.CreateWithBot(
            new RoomCode(RoomFactory.RoomCodeValue), module.Id, SideSelectionMode.HostPicksSpecific,
            BotRoomFactory.Host(FakeBotGameModule.First), BotRoomFactory.Bot() with { Kind = PlayerKind.Human },
            BotLevel.Easy, module, now);

        picks.Should().Throw<DomainException>();
        humanBot.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(FakeBotGameModule.First, 1)]
    [InlineData(FakeBotGameModule.Second, 0)]
    public async Task Host_offer_is_accepted_by_the_bot_at_once_with_sides_swapped(
        string hostSideInFirstMatch, int expectedBotTurns)
    {
        var module = new FakeBotGameModule();
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        var timeouts = new RecordingTimeoutScheduler();
        var botTurns = new RecordingBotTurnScheduler();
        var room = BotRoomFactory.Started(module, clock.UtcNow, hostSideInFirstMatch);
        room.CurrentMatch!.Resign(room.Host.Side!);
        room.EndCurrentMatch();
        rooms.Seed(room);

        var handler = new OfferRematchHandler(
            rooms, BotRoomFactory.Registry(module), clock, timeouts, new RecordingSetupDeadlineScheduler(),
            new RecordingRateLimiter(), TestHandlers.BotTurns(botTurns));

        var result = await handler.HandleAsync(
            new OfferRematchCommand(RoomFactory.RoomCodeValue, RoomFactory.HostPlayerId, Role.Host),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Effect.Should().Be(RematchOfferResult.ImplicitlyAccepted);
        var after = (await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), CancellationToken.None))!;
        after.Status.Should().Be(RoomStatus.InProgress);
        after.Host.Side.Should().Be(module.OtherSide(hostSideInFirstMatch));
        after.Challenger!.Side.Should().Be(hostSideInFirstMatch);
        after.Challenger.IsBot.Should().BeTrue();
        after.SeriesScore.Challenger.Should().Be(1, "the host resigned the first match");
        timeouts.Scheduled.Should().ContainSingle();
        botTurns.Scheduled.Should().HaveCount(expectedBotTurns);
    }
}
