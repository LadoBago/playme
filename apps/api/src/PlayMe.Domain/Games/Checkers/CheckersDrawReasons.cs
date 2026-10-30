namespace PlayMe.Domain.Games.Checkers;

/// <summary>
/// Rendering hints carried on a drawn terminal <see cref="CheckersState"/>
/// so the renderer can say <em>why</em> the match was drawn. Module
/// vocabulary agreed with the web renderer; the platform only sees the
/// <see cref="Platform.Draw"/> outcome.
/// </summary>
public static class CheckersDrawReasons
{
    /// <summary>The same position (board + side to move) occurred a third time.</summary>
    public const string Repetition = "repetition";

    /// <summary>Fifteen consecutive king-only, capture-free moves by each side.</summary>
    public const string KingMoves = "kingMoves";
}
