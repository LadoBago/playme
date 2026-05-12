namespace PlayMe.Domain.Platform;

/// <summary>
/// Outcome of <see cref="IGameModule.ApplyMove"/>. Either the move was
/// accepted (in which case <see cref="NewState"/> is set; <see cref="Ending"/>
/// is non-null only if the move ended the match) or rejected (in which case
/// <see cref="RejectReason"/> explains why and the state is unchanged).
/// </summary>
public sealed record MoveResult
{
    public bool Accepted { get; }
    public IGameState? NewState { get; }
    public Outcome? Ending { get; }
    public MoveRejectReason? RejectReason { get; }

    private MoveResult(
        bool accepted,
        IGameState? newState,
        Outcome? ending,
        MoveRejectReason? rejectReason)
    {
        Accepted = accepted;
        NewState = newState;
        Ending = ending;
        RejectReason = rejectReason;
    }

    public static MoveResult Accept(IGameState newState, Outcome? ending = null) =>
        new(true, newState, ending, null);

    public static MoveResult Reject(MoveRejectReason reason) =>
        new(false, null, null, reason);
}
