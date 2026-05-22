using System.Text.Json;
using FluentAssertions;
using PlayMe.Domain.Games.Connect4;
using PlayMe.Domain.Games.Reversi;
using PlayMe.Domain.Games.TicTacToe;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Pins the Sprint 9 platform seam: modules without configurable options
/// (Connect 4, Reversi) accept null and reject anything else; the
/// <see cref="Room"/> aggregate stores the supplied options immutably on
/// the aggregate without inspection. The unified <c>tictactoe</c> module's
/// own option-shape validation (boardSize ∈ {3,6,9}) lives in
/// <c>TicTacToeGameModuleTests</c>; that module is intentionally omitted
/// here because it requires non-null options by design.
/// </summary>
public sealed class GameOptionsSeamTests
{
    public static IEnumerable<object[]> OptionlessModules() => new[]
    {
        new object[] { (IGameModule)new Connect4GameModule() },
        new object[] { (IGameModule)new ReversiGameModule() },
    };

    [Theory]
    [MemberData(nameof(OptionlessModules))]
    public void ValidateOptions_accepts_null_for_modules_without_options(IGameModule module)
    {
        module.ValidateOptions(null).Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(OptionlessModules))]
    public void ValidateOptions_rejects_non_null_for_modules_without_options(IGameModule module)
    {
        var options = JsonDocument.Parse("""{"anything": "at all"}""").RootElement;

        module.ValidateOptions(options).Should().Be("errors.config.invalidGameOptions");
    }

    [Fact]
    public void Room_Create_stores_GameOptions_opaquely()
    {
        // The platform never inspects the blob — only the module does. This
        // pins that contract: any well-formed JsonElement passes through
        // Room.Create unchanged.
        var options = JsonDocument.Parse("""{"boardSize": 9, "winLength": 5}""").RootElement;
        var host = new Player(
            new PlayerId("host-player"),
            DisplayName.Create("Host"),
            TicTacToeSides.X);

        var room = Room.Create(
            new RoomCode("ABCDEF"),
            TicTacToeGameModule.GameId,
            SideSelectionMode.HostPicksSpecific,
            host,
            DateTimeOffset.UtcNow,
            gameOptions: options);

        room.GameOptions.Should().NotBeNull();
        room.GameOptions!.Value.GetProperty("boardSize").GetInt32().Should().Be(9);
        room.GameOptions!.Value.GetProperty("winLength").GetInt32().Should().Be(5);
    }

    [Fact]
    public void Room_Create_defaults_GameOptions_to_null_for_optionless_games()
    {
        // Reversi / Connect 4 callers omit gameOptions entirely; the
        // optional-default at the end of Room.Create keeps every existing
        // call site source-compatible.
        var host = new Player(
            new PlayerId("host-player"),
            DisplayName.Create("Host"),
            ReversiSides.Dark);

        var room = Room.Create(
            new RoomCode("ABCDEF"),
            ReversiGameModule.GameId,
            SideSelectionMode.HostPicksSpecific,
            host,
            DateTimeOffset.UtcNow);

        room.GameOptions.Should().BeNull();
    }
}
