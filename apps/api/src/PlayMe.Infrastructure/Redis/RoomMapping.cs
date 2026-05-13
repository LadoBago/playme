using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;

namespace PlayMe.Infrastructure.Redis;

/// <summary>
/// Bidirectional mapping between the <see cref="Room"/> domain aggregate and
/// its <see cref="RoomRecord"/> persisted form. Per-game state encoding is
/// delegated to <see cref="IGameModule.Serialize"/> / <see cref="IGameModule.Deserialize"/>
/// — this layer never knows board shape (CLAUDE.md §7 "Platform thinness").
/// </summary>
internal static class RoomMapping
{
    public static RoomRecord ToRecord(Room room, IGameModuleRegistry games) => new(
        Code: room.Code,
        GameId: room.GameId,
        SideSelectionMode: room.SideSelectionMode,
        CreatedAt: room.CreatedAt,
        Host: ToPlayerRecord(room.Host),
        Challenger: room.Challenger is null ? null : ToPlayerRecord(room.Challenger),
        Status: room.Status,
        CurrentMatch: room.CurrentMatch is null
            ? null
            : ToMatchRecord(room.CurrentMatch, games.GetModule(room.GameId)),
        HostConnected: room.HostConnected,
        ChallengerConnected: room.ChallengerConnected);

    public static Room FromRecord(RoomRecord record, IGameModuleRegistry games) => Room.Rehydrate(
        code: record.Code,
        gameId: record.GameId,
        sideSelectionMode: record.SideSelectionMode,
        createdAt: record.CreatedAt,
        host: FromPlayerRecord(record.Host),
        challenger: record.Challenger is null ? null : FromPlayerRecord(record.Challenger),
        status: record.Status,
        currentMatch: record.CurrentMatch is null
            ? null
            : FromMatchRecord(record.CurrentMatch, games.GetModule(record.GameId)),
        hostConnected: record.HostConnected,
        challengerConnected: record.ChallengerConnected);

    private static PlayerRecord ToPlayerRecord(Player player) =>
        new(player.Id, player.DisplayName, player.Side);

    private static Player FromPlayerRecord(PlayerRecord record) =>
        new(record.Id, record.DisplayName, record.Side);

    private static MatchRecord ToMatchRecord(Match match, IGameModule module) => new(
        GameId: match.GameId,
        SideToMove: match.SideToMove,
        MoveCount: match.MoveCount,
        State: module.Serialize(match.State),
        HostClockMs: (long)match.Clock.HostRemaining.TotalMilliseconds,
        ChallengerClockMs: (long)match.Clock.ChallengerRemaining.TotalMilliseconds,
        ActivePlayer: match.Clock.ActivePlayer,
        LastTickAt: match.Clock.LastTickAt,
        Outcome: match.Outcome is null ? null : ToOutcomeRecord(match.Outcome));

    private static Match FromMatchRecord(MatchRecord record, IGameModule module) => Match.Rehydrate(
        gameId: record.GameId,
        state: module.Deserialize(record.State),
        sideToMove: record.SideToMove,
        moveCount: record.MoveCount,
        clock: new MatchClock(
            HostRemaining: TimeSpan.FromMilliseconds(record.HostClockMs),
            ChallengerRemaining: TimeSpan.FromMilliseconds(record.ChallengerClockMs),
            ActivePlayer: record.ActivePlayer,
            LastTickAt: record.LastTickAt),
        outcome: record.Outcome is null ? null : FromOutcomeRecord(record.Outcome));

    private static OutcomeRecord ToOutcomeRecord(Outcome outcome) => outcome switch
    {
        Win w => new OutcomeRecord("win", w.WinningSide, ResigningSide: null, TimedOutSide: null, WinningLine: w.WinningLine),
        Draw => new OutcomeRecord("draw", WinningSide: null, ResigningSide: null, TimedOutSide: null, WinningLine: null),
        Resign r => new OutcomeRecord("resign", WinningSide: null, ResigningSide: r.ResigningSide, TimedOutSide: null, WinningLine: null),
        Domain.Platform.Timeout t => new OutcomeRecord("timeout", WinningSide: null, ResigningSide: null, TimedOutSide: t.TimedOutSide, WinningLine: null),
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
        "timeout" => new Domain.Platform.Timeout(
            record.TimedOutSide ?? throw new InvalidOperationException("Timeout outcome missing timedOutSide.")),
        _ => throw new InvalidOperationException($"Unknown outcome kind '{record.Kind}'."),
    };
}
