using System.Diagnostics.CodeAnalysis;
using PlayMe.Domain.Games.TicTacToe3x3;
using PlayMe.Domain.Platform;

namespace PlayMe.Infrastructure.Redis;

/// <summary>
/// Bidirectional mapping between the <see cref="Room"/> domain aggregate and
/// its <see cref="RoomRecord"/> persisted form. Game-state flattening lives
/// here; the Domain doesn't know about the storage shape. New game modules
/// extend the per-game cases in <see cref="EncodeState"/> / <see cref="DecodeState"/>.
/// </summary>
internal static class RoomMapping
{
    public static RoomRecord ToRecord(Room room) => new(
        Code: room.Code,
        GameId: room.GameId,
        SideSelectionMode: room.SideSelectionMode,
        CreatedAt: room.CreatedAt,
        Host: ToPlayerRecord(room.Host),
        Challenger: room.Challenger is null ? null : ToPlayerRecord(room.Challenger),
        Status: room.Status,
        CurrentMatch: room.CurrentMatch is null ? null : ToMatchRecord(room.CurrentMatch),
        HostConnected: room.HostConnected,
        ChallengerConnected: room.ChallengerConnected);

    public static Room FromRecord(RoomRecord record) => Room.Rehydrate(
        code: record.Code,
        gameId: record.GameId,
        sideSelectionMode: record.SideSelectionMode,
        createdAt: record.CreatedAt,
        host: FromPlayerRecord(record.Host),
        challenger: record.Challenger is null ? null : FromPlayerRecord(record.Challenger),
        status: record.Status,
        currentMatch: record.CurrentMatch is null ? null : FromMatchRecord(record.CurrentMatch),
        hostConnected: record.HostConnected,
        challengerConnected: record.ChallengerConnected);

    private static PlayerRecord ToPlayerRecord(Player player) =>
        new(player.Id, player.DisplayName, player.Side);

    private static Player FromPlayerRecord(PlayerRecord record) =>
        new(record.Id, record.DisplayName, record.Side);

    private static MatchRecord ToMatchRecord(Match match)
    {
        var (rows, cols, cells) = EncodeState(match.GameId, match.State);
        return new MatchRecord(
            GameId: match.GameId,
            SideToMove: match.SideToMove,
            MoveCount: match.MoveCount,
            StateRows: rows,
            StateCols: cols,
            StateCells: cells,
            Outcome: match.Outcome is null ? null : ToOutcomeRecord(match.Outcome));
    }

    private static Match FromMatchRecord(MatchRecord record) => Match.Rehydrate(
        gameId: record.GameId,
        state: DecodeState(record.GameId, record.StateRows, record.StateCols, record.StateCells),
        sideToMove: record.SideToMove,
        moveCount: record.MoveCount,
        outcome: record.Outcome is null ? null : FromOutcomeRecord(record.Outcome));

    private static OutcomeRecord ToOutcomeRecord(Outcome outcome) => outcome switch
    {
        Win w => new OutcomeRecord("win", w.WinningSide, ResigningSide: null, WinningLine: w.WinningLine),
        Draw => new OutcomeRecord("draw", WinningSide: null, ResigningSide: null, WinningLine: null),
        Resign r => new OutcomeRecord("resign", WinningSide: null, ResigningSide: r.ResigningSide, WinningLine: null),
        _ => throw new InvalidOperationException($"Unsupported outcome '{outcome.GetType().Name}'."),
    };

    private static Outcome FromOutcomeRecord(OutcomeRecord record) => record.Kind switch
    {
        "win" => new Win(
            record.WinningSide ?? throw new InvalidOperationException("Win outcome missing winningSide."),
            record.WinningLine ?? throw new InvalidOperationException("Win outcome missing winningLine.")),
        "draw" => new Draw(),
        "resign" => new Resign(
            record.ResigningSide ?? throw new InvalidOperationException("Resign outcome missing resigningSide.")),
        _ => throw new InvalidOperationException($"Unknown outcome kind '{record.Kind}'."),
    };

    private static (int Rows, int Cols, IReadOnlyList<string?> Cells) EncodeState(
        GameId gameId, IGameState state)
    {
        if (state is TicTacToe3x3State ttt)
        {
            return (TicTacToe3x3State.Size, TicTacToe3x3State.Size, ttt.Cells);
        }
        throw new InvalidOperationException(
            $"No state encoder registered for game '{gameId}'.");
    }

    [SuppressMessage("Performance", "CA1859:Use concrete types when possible",
        Justification = "Polymorphic per-game dispatch; concrete return type would defeat the abstraction once more games register.")]
    private static IGameState DecodeState(
        GameId gameId, int rows, int cols, IReadOnlyList<string?> cells)
    {
        if (gameId == TicTacToe3x3GameModule.GameId)
        {
            if (rows != TicTacToe3x3State.Size ||
                cols != TicTacToe3x3State.Size ||
                cells.Count != TicTacToe3x3State.CellCount)
            {
                throw new InvalidOperationException(
                    $"TicTacToe3x3 state shape mismatch: rows={rows}, cols={cols}, cells={cells.Count}.");
            }
            return new TicTacToe3x3State(cells);
        }
        throw new InvalidOperationException(
            $"No state decoder registered for game '{gameId}'.");
    }
}
