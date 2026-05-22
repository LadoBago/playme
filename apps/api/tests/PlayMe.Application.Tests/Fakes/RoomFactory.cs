using System.Text.Json;
using PlayMe.Domain.Games.TicTacToe;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Tests.Fakes;

/// <summary>
/// Builds a <see cref="Room"/> with both players seated and an active
/// match — the typical fixture for SubmitMove / clock tests. Sides are
/// resolved via the canonical first-mover (host plays X, challenger
/// plays O). Uses the unified Tic-Tac-Toe module with the 3×3 board
/// size for parity with the original Sprint 1 fixture (Sprint 9 PR3).
/// </summary>
public static class RoomFactory
{
    public const string HostPlayerId = "host-player";
    public const string ChallengerPlayerId = "challenger-player";
    public const string RoomCodeValue = "ABCDEF";

    /// <summary>
    /// Shared <c>boardSize: 3</c> options blob used by every fixture in
    /// this file — JsonDocument is read-once-keep-alive, so we hand out
    /// fresh JsonElement instances from a parsed parent each call.
    /// </summary>
    public static JsonElement DefaultGameOptions() =>
        JsonDocument.Parse("""{"boardSize":3}""").RootElement;

    public static Room InProgress(DateTimeOffset startedAt, TimeSpan clockBudget)
    {
        var room = Room.Create(
            new RoomCode(RoomCodeValue),
            TicTacToeGameModule.GameId,
            SideSelectionMode.HostPicksSpecific,
            new Player(
                new PlayerId(HostPlayerId),
                DisplayName.Create("Host"),
                TicTacToeSides.X),
            startedAt,
            gameOptions: DefaultGameOptions());

        var module = new TicTacToeGameModule();
        room.RegisterChallenger(
            new Player(
                new PlayerId(ChallengerPlayerId),
                DisplayName.Create("Challenger"),
                Side: null),
            challengerPickedSide: null,
            module);

        room.MarkConnected(Role.Host);
        room.MarkConnected(Role.Challenger);
        room.TryStartMatch(module, clockBudget, startedAt);
        return room;
    }

    /// <summary>
    /// Builds a <see cref="Room"/> in <see cref="RoomStatus.WaitingForOpponent"/>
    /// — host registered, no challenger, no match. Mirrors the post-
    /// CreateRoomHandler state used by the room-expiry tests.
    /// </summary>
    public static Room WaitingForOpponent(DateTimeOffset createdAt) =>
        Room.Create(
            new RoomCode(RoomCodeValue),
            TicTacToeGameModule.GameId,
            SideSelectionMode.HostPicksSpecific,
            new Player(
                new PlayerId(HostPlayerId),
                DisplayName.Create("Host"),
                TicTacToeSides.X),
            createdAt,
            gameOptions: DefaultGameOptions());
}
