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
            Clock: ToClockSnapshotDto(match.Clock, now, match.IsEnded),
            Outcome: match.Outcome is null ? null : ToOutcomeDto(match.Outcome));
    }

    /// <summary>
    /// Serialize the clock as values <em>effective at <paramref name="now"/></em>,
    /// not as raw stored values. The wire contract is "<c>hostMs</c> is the
    /// remaining time at <c>serverNowAt</c>" — the client extrapolates
    /// from there using its local clock delta. If we shipped the raw
    /// stored values (which are "as of <c>lastTickAt</c>"), a snapshot
    /// produced mid-turn (e.g. an HTTP <c>getRoom</c> during the active
    /// player's move) would top the clock back up to its pre-move value
    /// on the client, then jump to zero when the server's timeout
    /// sweeper actually fires. <paramref name="matchEnded"/> short-
    /// circuits the extrapolation: an ended match has a frozen clock,
    /// so we ship the stored values unchanged.
    /// </summary>
    public static ClockSnapshotDto ToClockSnapshotDto(
        MatchClock clock, DateTimeOffset now, bool matchEnded)
    {
        var hostRemaining = matchEnded
            ? clock.HostRemaining
            : clock.EffectiveRemaining(Role.Host, now);
        var challengerRemaining = matchEnded
            ? clock.ChallengerRemaining
            : clock.EffectiveRemaining(Role.Challenger, now);

        return new ClockSnapshotDto(
            HostMs: (long)hostRemaining.TotalMilliseconds,
            ChallengerMs: (long)challengerRemaining.TotalMilliseconds,
            ActivePlayer: clock.ActivePlayer.ToString().ToLowerInvariant(),
            LastTickAt: clock.LastTickAt,
            ServerNowAt: now);
    }

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
