namespace PlayMe.Domain.Games.Checkers;

/// <summary>
/// Cell tokens of the Checkers board and the helpers that read them. A cell
/// is <c>null</c> (empty) or one of four piece tokens — the same strings
/// the serialized state carries to the web renderer, so the wire shape and
/// the in-memory shape never drift. Module-owned vocabulary; the platform
/// never inspects it (CLAUDE.md §7 "Platform thinness").
/// </summary>
public static class CheckersPieces
{
    public const string WhiteMan = "white";
    public const string BlackMan = "black";
    public const string WhiteKing = "white-king";
    public const string BlackKing = "black-king";

    /// <summary>Owning side of a cell token, or null for an empty cell.</summary>
    public static string? SideOf(string? piece) => piece switch
    {
        WhiteMan or WhiteKing => CheckersSides.White,
        BlackMan or BlackKing => CheckersSides.Black,
        _ => null,
    };

    public static bool IsKing(string? piece) => piece is WhiteKing or BlackKing;

    public static string ManOf(string side) =>
        side == CheckersSides.White ? WhiteMan : BlackMan;

    public static string KingOf(string side) =>
        side == CheckersSides.White ? WhiteKing : BlackKing;

    /// <summary>True for an empty cell or any of the four piece tokens.</summary>
    public static bool IsValidCell(string? piece) =>
        piece is null or WhiteMan or BlackMan or WhiteKing or BlackKing;
}
