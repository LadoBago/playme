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

    private MoveResult(
        bool accepted,
        IGameState? newState,
        Outcome? ending,
        string? rejectKey)
    {
        Accepted = accepted;
        NewState = newState;
        Ending = ending;
        RejectKey = rejectKey;
    }

    public static MoveResult Accept(IGameState newState, Outcome? ending = null) =>
        new(true, newState, ending, null);

    public static MoveResult Reject(string rejectKey) =>
        new(false, null, null, rejectKey);
}
