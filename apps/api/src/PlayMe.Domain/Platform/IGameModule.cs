namespace PlayMe.Domain.Platform;

/// <summary>
/// Minimal hook for a self-contained game module (CLAUDE.md §2.3, SOLID
/// open/closed in §8). Each game implements this; the platform's move
/// pipeline dispatches by <see cref="Id"/> and otherwise treats the module
/// as opaque rules. **No shared rules engine across modules.**
/// </summary>
public interface IGameModule
{
    GameId Id { get; }

    /// <summary>
    /// The two side identifiers this game uses. Exactly two entries; lower-
    /// case ("x"/"o" for Tic-Tac-Toe, "red"/"yellow" for Connect 4 — §2.3 #14).
    /// </summary>
    IReadOnlyList<string> ValidSides { get; }

    /// <summary>
    /// Side that moves first by canonical rule (CLAUDE.md §2.3 #11):
    /// "x" for every Tic-Tac-Toe variant, "red" for Connect 4. Must be one
    /// of <see cref="ValidSides"/>.
    /// </summary>
    string FirstMoveSide { get; }

    /// <summary>
    /// Per-side starting clock budget for this game. The platform reads this
    /// when a match starts; it never enumerates per-game budgets itself
    /// (CLAUDE.md §7 "Platform thinness").
    /// </summary>
    TimeSpan DefaultClockBudget { get; }

    /// <summary>Initial state for a new match (empty board).</summary>
    IGameState NewMatch();

    /// <summary>
    /// Validate and apply a move on behalf of <paramref name="side"/>. The
    /// caller (<c>SubmitMoveHandler</c>) has already verified that the room
    /// is in progress and that <paramref name="side"/> is the side-to-move;
    /// this method only judges the move's legality against the rules and
    /// detects win/draw post-application.
    /// </summary>
    MoveResult ApplyMove(IGameState state, string side, GameMove move);

    /// <summary>Side opposite to <paramref name="side"/>.</summary>
    string OtherSide(string side);

    /// <summary>
    /// Serialize <paramref name="state"/> to an opaque JSON string. Used by
    /// both persistence (Redis room blob) and the wire (`MatchDto.State`)
    /// without the platform inspecting the shape — round-tripping through
    /// <see cref="Deserialize"/> is the module's responsibility. The string
    /// is rendered by the per-game web renderer; the platform never reads
    /// it (CLAUDE.md §7 "Platform thinness").
    /// </summary>
    string Serialize(IGameState state);

    /// <summary>
    /// Reverse of <see cref="Serialize"/>. Throws <see cref="ArgumentException"/>
    /// if the blob can't be parsed as this game's state.
    /// </summary>
    IGameState Deserialize(string serialized);
}
