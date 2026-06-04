namespace PlayMe.Domain.Games.SeaBattle;

/// <summary>
/// One ship: a straight horizontal or vertical line of
/// <see cref="Length"/> cells anchored at (<see cref="X"/>, <see cref="Y"/>)
/// (top-left end). Straightness is by construction — placement freedom is
/// only the anchor, length, and orientation.
/// </summary>
public readonly record struct SeaBattleShip(int X, int Y, int Length, bool Horizontal)
{
    /// <summary>The cells this ship occupies, anchor first.</summary>
    public IEnumerable<SeaBattleCoordinate> Cells()
    {
        for (var i = 0; i < Length; i++)
        {
            yield return Horizontal
                ? new SeaBattleCoordinate(X + i, Y)
                : new SeaBattleCoordinate(X, Y + i);
        }
    }
}
