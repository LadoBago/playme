using FluentAssertions;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Commands.CreateRoom;
using PlayMe.Application.Errors;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Seam D (Sprint 12): creating a bot room. The seat is filled and
/// connected at creation, the level is stored, and the three rejections
/// — module without a bot, missing level, challenger-picks sides — come
/// back as config errors. Bot-capability is derived from the module's
/// interfaces (<see cref="CreateRoomHandler.SupportsBot"/>), never from
/// a list.
/// </summary>
public sealed class CreateRoomHandlerBotTests
{
    private sealed class FixedCodes : IRoomCodeGenerator
    {
        public RoomCode NewCode() => new(RoomFactory.RoomCodeValue);
    }

    private sealed class SequentialPlayerIds : IPlayerIdGenerator
    {
        private int _n;

        public PlayerId NewPlayerId() => new($"player-{++_n}");
    }

    private static CreateRoomHandler Handler(IGameModuleRegistry registry, FakeRoomRepository rooms) =>
        new(
            rooms,
            new FixedCodes(),
            new SequentialPlayerIds(),
            registry,
            new SeededRandom(),
            new FakeClock(),
            new RecordingRoomExpiryScheduler());

    private static CreateRoomCommand BotRoom(
        GameId gameId,
        SideSelectionMode mode = SideSelectionMode.HostPicksSpecific,
        string? hostSide = FakeBotGameModule.First,
        BotLevel? level = BotLevel.Hard) =>
        new("Host", gameId.Value, mode, hostSide, GameOptions: null, Opponent: PlayerKind.Bot, BotLevel: level);

    [Fact]
    public async Task Bot_room_seats_a_connected_bot_on_the_opposite_side()
    {
        var module = new FakeBotGameModule();
        var rooms = new FakeRoomRepository();

        var result = await Handler(BotRoomFactory.Registry(module), rooms)
            .HandleAsync(BotRoom(module.Id), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var room = (await rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), CancellationToken.None))!;
        room.HasBotChallenger.Should().BeTrue();
        room.Challenger!.Side.Should().Be(FakeBotGameModule.Second);
        room.Challenger.Id.Value.Should().NotBe(room.Host.Id.Value);
        room.ChallengerConnected.Should().BeTrue();
        room.HostConnected.Should().BeFalse();
        room.Status.Should().Be(RoomStatus.WaitingForOpponent, "until the host's presence registers");
        room.BotLevel.Should().Be(BotLevel.Hard);

        result.Value!.Room.Challenger!.Kind.Should().Be(PlayerKind.Bot);
        result.Value.Room.BotLevel.Should().Be(BotLevel.Hard);
    }

    [Fact]
    public async Task Random_side_mode_still_works_for_a_bot_room()
    {
        var module = new FakeBotGameModule();
        var rooms = new FakeRoomRepository();

        var result = await Handler(BotRoomFactory.Registry(module), rooms)
            .HandleAsync(BotRoom(module.Id, SideSelectionMode.Random, hostSide: null), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var room = result.Value!.Room;
        room.Host.Side.Should().NotBeNull();
        room.Challenger!.Side.Should().Be(module.OtherSide(room.Host.Side!));
    }

    [Fact]
    public async Task Module_without_a_bot_is_rejected()
    {
        var module = new FakeKeepTurnModule();
        var registry = new StubModuleRegistry(module, new FakeKeepTurnMoveParser());

        var result = await Handler(registry, new FakeRoomRepository())
            .HandleAsync(BotRoom(module.Id, hostSide: "first"), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(PlatformErrors.ConfigBotNotSupported);
    }

    [Fact]
    public async Task Missing_level_is_rejected()
    {
        var module = new FakeBotGameModule();

        var result = await Handler(BotRoomFactory.Registry(module), new FakeRoomRepository())
            .HandleAsync(BotRoom(module.Id, level: null), CancellationToken.None);

        result.Error.Should().Be(PlatformErrors.ConfigInvalidBotLevel);
    }

    [Fact]
    public async Task Level_on_a_human_room_is_rejected()
    {
        var module = new FakeBotGameModule();
        var cmd = new CreateRoomCommand(
            "Host", module.Id.Value, SideSelectionMode.HostPicksSpecific, FakeBotGameModule.First,
            GameOptions: null, Opponent: PlayerKind.Human, BotLevel: BotLevel.Easy);

        var result = await Handler(BotRoomFactory.Registry(module), new FakeRoomRepository())
            .HandleAsync(cmd, CancellationToken.None);

        result.Error.Should().Be(PlatformErrors.ConfigInvalidBotLevel);
    }

    [Fact]
    public async Task Challenger_picks_is_rejected_for_a_bot_room()
    {
        var module = new FakeBotGameModule();

        var result = await Handler(BotRoomFactory.Registry(module), new FakeRoomRepository())
            .HandleAsync(BotRoom(module.Id, SideSelectionMode.ChallengerPicks, hostSide: null), CancellationToken.None);

        result.Error.Should().Be(PlatformErrors.ConfigInvalidSideSelectionMode);
    }

    [Fact]
    public void Bot_capability_is_derived_from_the_module_interfaces()
    {
        CreateRoomHandler.SupportsBot(new FakeBotGameModule()).Should().BeTrue();
        CreateRoomHandler.SupportsBot(new FakeBotSetupGameModule()).Should().BeTrue();
        CreateRoomHandler.SupportsBot(new FakeKeepTurnModule()).Should().BeFalse();
        CreateRoomHandler.SupportsBot(new FakeSetupGameModule()).Should().BeFalse(
            "a setup game needs IBotSetup as well as IBotPlayer");
    }
}
