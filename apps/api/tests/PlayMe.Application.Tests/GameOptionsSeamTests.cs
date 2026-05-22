using System.Text.Json;
using FluentAssertions;
using PlayMe.Domain.Games.Connect4;
using PlayMe.Domain.Games.Reversi;
using PlayMe.Domain.Games.TicTacToe3x3;
using PlayMe.Domain.Games.TicTacToe6x6;
using PlayMe.Domain.Games.TicTacToe9x9;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Pins the Sprint 9 PR1 platform seam: every existing module rejects
/// non-null per-room <c>gameOptions</c> (no module accepts options yet —
/// PR1b adds the unified <c>tictactoe</c> module that does), and the
/// <see cref="Room"/> aggregate stores the supplied options immutably on
/// the aggregate without inspection.
/// </summary>
public sealed class GameOptionsSeamTests
{
    public static IEnumerable<object[]> ExistingModules() => new[]
    {
        new object[] { (IGameModule)new TicTacToe3x3GameModule() },
        new object[] { (IGameModule)new TicTacToe6x6GameModule() },
        new object[] { (IGameModule)new TicTacToe9x9GameModule() },
        new object[] { (IGameModule)new Connect4GameModule() },
        new object[] { (IGameModule)new ReversiGameModule() },
    };

    [Theory]
    [MemberData(nameof(ExistingModules))]
    public void ValidateOptions_accepts_null_for_modules_without_options(IGameModule module)
    {
        module.ValidateOptions(null).Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(ExistingModules))]
    public void ValidateOptions_rejects_non_null_for_modules_without_options(IGameModule module)
    {
        var options = JsonDocument.Parse("""{"boardSize": 6}""").RootElement;

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
            TicTacToe3x3GameModule.GameId,
            SideSelectionMode.HostPicksSpecific,
            host,
            DateTimeOffset.UtcNow,
            gameOptions: options);

        room.GameOptions.Should().NotBeNull();
        room.GameOptions!.Value.GetProperty("boardSize").GetInt32().Should().Be(9);
        room.GameOptions!.Value.GetProperty("winLength").GetInt32().Should().Be(5);
    }

    [Fact]
    public void Room_Create_defaults_GameOptions_to_null()
    {
        // Existing 5 modules' callers (and the four current games) omit
        // gameOptions entirely; the optional-default keeps every existing
        // call site source-compatible.
        var host = new Player(
            new PlayerId("host-player"),
            DisplayName.Create("Host"),
            TicTacToeSides.X);

        var room = Room.Create(
            new RoomCode("ABCDEF"),
            TicTacToe3x3GameModule.GameId,
            SideSelectionMode.HostPicksSpecific,
            host,
            DateTimeOffset.UtcNow);

        room.GameOptions.Should().BeNull();
    }
}
