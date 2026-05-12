namespace PlayMe.Domain.Platform;

/// <summary>
/// One round of gameplay inside a <see cref="Room"/> (CLAUDE.md §2.7). Holds
/// the opaque per-game <see cref="State"/>, the side whose turn it is, and
/// the <see cref="Outcome"/> once the match terminates. Resigns are recorded
/// directly via <see cref="Resign"/>; move-driven endings flow through
/// <see cref="ApplyAcceptedMove"/> after the module judges the move.
/// </summary>
public sealed class Match
{
    public GameId GameId { get; }
    public IGameState State { get; private set; }
    public string SideToMove { get; private set; }
    public int MoveCount { get; private set; }
    public Outcome? Outcome { get; private set; }

    public bool IsEnded => Outcome is not null;

    private Match(GameId gameId, IGameState state, string sideToMove, int moveCount, Outcome? outcome)
    {
        GameId = gameId;
        State = state;
        SideToMove = sideToMove;
        MoveCount = moveCount;
        Outcome = outcome;
    }

    public static Match Start(GameId gameId, IGameState initialState, string firstMoveSide) =>
        new(gameId, initialState, firstMoveSide, moveCount: 0, outcome: null);

    /// <summary>
    /// Rehydrate a match snapshot from persistence. Used by the Infrastructure
    /// layer; application code constructs matches via <see cref="Start"/>.
    /// </summary>
    public static Match Rehydrate(
        GameId gameId,
        IGameState state,
        string sideToMove,
        int moveCount,
        Outcome? outcome) =>
        new(gameId, state, sideToMove, moveCount, outcome);

    /// <summary>
    /// Commit an accepted move's effect: swap the side to move, increment the
    /// counter, and record the ending if the move terminated the match.
    /// </summary>
    public void ApplyAcceptedMove(IGameState newState, string nextSideToMove, Outcome? ending)
    {
        if (IsEnded)
        {
            throw new DomainException("Cannot apply a move to a finished match.");
        }

        State = newState;
        SideToMove = nextSideToMove;
        MoveCount++;
        Outcome = ending;
    }

    public void Resign(string resigningSide)
    {
        if (IsEnded)
        {
            throw new DomainException("Cannot resign a finished match.");
        }

        Outcome = new Resign(resigningSide);
    }
}
