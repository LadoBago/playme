namespace PlayMe.Domain.Platform;

/// <summary>
/// Session-only win counter that survives rematches inside a single
/// <see cref="Room"/> (docs/platform-and-games.md §1 #13). Win = 1 point;
/// Draw = 0 points but is tracked for display context. <c>Resign</c>,
/// <c>Timeout</c>, and <c>Disconnect</c> roll into the opponent's win
/// — from the player's POV "I won that game" reads the same regardless
/// of how the opponent's seat ended.
///
/// Lives in the room state in Redis and is discarded when the room
/// reaches <see cref="RoomStatus.Closed"/> or <see cref="RoomStatus.Expired"/>.
/// No persistence beyond the room.
/// </summary>
public sealed record SeriesScore(int Host, int Challenger, int Draws)
{
    public static SeriesScore Zero { get; } = new(0, 0, 0);

    public SeriesScore WithWin(Role winner) => winner switch
    {
        Role.Host => this with { Host = Host + 1 },
        Role.Challenger => this with { Challenger = Challenger + 1 },
        _ => throw new DomainException($"Cannot record win for role '{winner}'."),
    };

    public SeriesScore WithDraw() => this with { Draws = Draws + 1 };

    /// <summary>Total matches recorded — useful for the in-match display
    /// ("3 played" subtitle, total-match counter) per §1 #13.</summary>
    public int TotalMatches => Host + Challenger + Draws;
}
