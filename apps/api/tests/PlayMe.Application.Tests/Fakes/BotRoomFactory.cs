using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Tests.Fakes;

/// <summary>
/// Builds bot rooms (Sprint 12 seam D) around the <see cref="FakeBotGameModule"/>
/// family: host seated, bot seated by <see cref="Room.CreateWithBot"/>,
/// host connected, match started. With the setup variant the room lands
/// in <see cref="RoomStatus.SettingUp"/>; otherwise in
/// <see cref="RoomStatus.InProgress"/>.
/// </summary>
public static class BotRoomFactory
{
    public const string BotPlayerId = "bot-player";

    public static Player Host(string side) =>
        new(new PlayerId(RoomFactory.HostPlayerId), DisplayName.Create("Host"), side);

    public static Player Bot() =>
        new(
            new PlayerId(BotPlayerId),
            DisplayName.Create(PlatformConstants.BotDisplayName),
            Side: null,
            PlayerKind.Bot);

    /// <summary>A bot room where the host plays <paramref name="hostSide"/>
    /// and the host has connected, so the match (or setup) has begun.</summary>
    public static Room Started(
        FakeBotGameModule module,
        DateTimeOffset now,
        string hostSide = FakeBotGameModule.First,
        BotLevel level = BotLevel.Medium)
    {
        var room = Room.CreateWithBot(
            new RoomCode(RoomFactory.RoomCodeValue),
            module.Id,
            SideSelectionMode.HostPicksSpecific,
            Host(hostSide),
            Bot(),
            level,
            module,
            now,
            gameOptions: null);
        room.MarkConnected(Role.Host);
        room.TryStartMatch(module, module.ClockBudgetFor(null), now);
        return room;
    }

    public static IGameModuleRegistry Registry(FakeBotGameModule module) =>
        new StubModuleRegistry(module, new FakeBotMoveParser(module.Id));
}

/// <summary>Seeded <see cref="IRandom"/> so bot decisions are reproducible.</summary>
public sealed class SeededRandom : IRandom
{
    private readonly System.Random _random;

    public SeededRandom(int seed = 42)
    {
        _random = new System.Random(seed);
    }

    public int NextInt(int maxExclusive) => _random.Next(maxExclusive);
}
