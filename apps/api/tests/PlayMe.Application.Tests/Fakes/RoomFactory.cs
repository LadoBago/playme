using PlayMe.Domain.Games.TicTacToe3x3;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Tests.Fakes;

/// <summary>
/// Builds a <see cref="Room"/> with both players seated and an active
/// match — the typical fixture for SubmitMove / clock tests. Sides are
/// resolved via the canonical first-mover (host plays X, challenger
/// plays O).
/// </summary>
public static class RoomFactory
{
    public const string HostPlayerId = "host-player";
    public const string ChallengerPlayerId = "challenger-player";
    public const string RoomCodeValue = "ABCDEF";

    public static Room InProgress(DateTimeOffset startedAt, TimeSpan clockBudget)
    {
        var room = Room.Create(
            new RoomCode(RoomCodeValue),
            TicTacToe3x3GameModule.GameId,
            SideSelectionMode.HostPicksSpecific,
            new Player(
                new PlayerId(HostPlayerId),
                DisplayName.Create("Host"),
                TicTacToeSides.X),
            startedAt);

        var module = new TicTacToe3x3GameModule();
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
            TicTacToe3x3GameModule.GameId,
            SideSelectionMode.HostPicksSpecific,
            new Player(
                new PlayerId(HostPlayerId),
                DisplayName.Create("Host"),
                TicTacToeSides.X),
            createdAt);
}
