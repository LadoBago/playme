using PlayMe.Application.Abstractions;
using PlayMe.Application.Dtos;
using PlayMe.Domain.Games.TicTacToe3x3;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Mapping;

/// <summary>
/// Maps Domain aggregates to wire DTOs. Lives in Application (not Api)
/// because handler return types are DTOs (CLAUDE.md §8: "Never expose Domain
/// entities directly through controllers or hubs").
///
/// Per-game state shape is encoded in <see cref="MapState"/>: each game
/// flattens its <c>IGameState</c> into a row-major <c>cells</c> array with
/// the right dimensions. New games extend the switch.
///
/// Mapping a <see cref="Room"/> requires a <see cref="DateTimeOffset"/>
/// "now" so the <see cref="ClockSnapshotDto"/> can stamp <c>serverNowAt</c>
/// — handlers pass <c>IClock.UtcNow</c>.
/// </summary>
public static class RoomMapper
{
    public static RoomDto ToDto(Room room, DateTimeOffset now) => new(
        Code: room.Code,
        GameId: room.GameId,
        SideSelectionMode: room.SideSelectionMode,
        Status: room.Status,
        Host: ToPlayerDto(room.Host),
        Challenger: room.Challenger is null ? null : ToPlayerDto(room.Challenger),
        HostConnected: room.HostConnected,
        ChallengerConnected: room.ChallengerConnected,
        CurrentMatch: room.CurrentMatch is null ? null : ToMatchDto(room.CurrentMatch, now),
        CreatedAt: room.CreatedAt);

    public static PlayerDto ToPlayerDto(Player player) =>
        new(player.DisplayName.Value, player.Side);

    public static MatchDto ToMatchDto(Match match, DateTimeOffset now)
    {
        var (rows, cols, cells) = MapState(match.GameId, match.State);
        return new MatchDto(
            GameId: match.GameId,
            SideToMove: match.SideToMove,
            MoveCount: match.MoveCount,
            Rows: rows,
            Cols: cols,
            Cells: cells,
            Clock: ToClockSnapshotDto(match.Clock, now),
            Outcome: match.Outcome is null ? null : ToOutcomeDto(match.Outcome));
    }

    public static ClockSnapshotDto ToClockSnapshotDto(MatchClock clock, DateTimeOffset now) =>
        new(
            HostMs: (long)clock.HostRemaining.TotalMilliseconds,
            ChallengerMs: (long)clock.ChallengerRemaining.TotalMilliseconds,
            ActivePlayer: clock.ActivePlayer.ToString().ToLowerInvariant(),
            LastTickAt: clock.LastTickAt,
            ServerNowAt: now);

    public static OutcomeDto ToOutcomeDto(Outcome outcome) => outcome switch
    {
        Win w => new OutcomeDto(
            Kind: "win",
            WinningSide: w.WinningSide,
            ResigningSide: null,
            TimedOutSide: null,
            WinningLine: w.WinningLine),

        Draw => new OutcomeDto(
            Kind: "draw",
            WinningSide: null,
            ResigningSide: null,
            TimedOutSide: null,
            WinningLine: null),

        Resign r => new OutcomeDto(
            Kind: "resign",
            WinningSide: null,
            ResigningSide: r.ResigningSide,
            TimedOutSide: null,
            WinningLine: null),

        Domain.Platform.Timeout t => new OutcomeDto(
            Kind: "timeout",
            WinningSide: null,
            ResigningSide: null,
            TimedOutSide: t.TimedOutSide,
            WinningLine: null),

        _ => throw new InvalidOperationException(
            $"Unsupported outcome type {outcome.GetType().Name}."),
    };

    private static (int Rows, int Cols, IReadOnlyList<string?> Cells) MapState(
        GameId gameId, IGameState state)
    {
        if (state is TicTacToe3x3State ttt)
        {
            return (TicTacToe3x3State.Size, TicTacToe3x3State.Size, ttt.Cells);
        }

        throw new InvalidOperationException(
            $"No state mapper registered for game '{gameId}'.");
    }
}
