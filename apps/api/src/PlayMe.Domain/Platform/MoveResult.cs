namespace PlayMe.Domain.Platform;

/// <summary>
/// Outcome of <see cref="IGameModule.ApplyMove"/>. Either the move was
/// accepted (in which case <see cref="NewState"/> is set; <see cref="Ending"/>
/// is non-null only if the move ended the match) or rejected (in which case
/// <see cref="RejectKey"/> is an opaque string the per-game module emits and
/// the per-game web renderer interprets — CLAUDE.md §7 "Platform thinness":
/// the platform never enumerates reject reasons).
/// </summary>
public sealed record MoveResult
{
    public bool Accepted { get; }
    public IGameState? NewState { get; }
    public Outcome? Ending { get; }
    public string? RejectKey { get; }

    /// <summary>
    /// When true, the mover retains the turn (Sprint 10 seam B — sea
    /// battle's hit-shoots-again). The platform no longer guarantees strict
    /// alternation; the module decides retention per accepted move, the
    /// platform enforces whose turn it is. Chess-clock semantics are
    /// unchanged: the mover's elapsed time is committed on every accepted
    /// move and their clock keeps running across a retained turn. For
    /// rare, decision-free skips (Reversi's pass) prefer the renderer-
    /// emitted synthetic move instead — see docs/roadmap/sprint-08-reversi.md.
    /// Meaningless on a match-ending move.
    /// </summary>
    public bool KeepTurn { get; }

    private MoveResult(
        bool accepted,
        IGameState? newState,
        Outcome? ending,
        string? rejectKey,
        bool keepTurn)
    {
        Accepted = accepted;
        NewState = newState;
        Ending = ending;
        RejectKey = rejectKey;
        KeepTurn = keepTurn;
    }

    public static MoveResult Accept(
        IGameState newState, Outcome? ending = null, bool keepTurn = false) =>
        new(true, newState, ending, null, keepTurn);

    public static MoveResult Reject(string rejectKey) =>
        new(false, null, null, rejectKey, keepTurn: false);
}
