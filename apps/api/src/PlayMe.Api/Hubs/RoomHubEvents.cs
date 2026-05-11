namespace PlayMe.Api.Hubs;

/// <summary>
/// Names of the server → client events broadcast from <see cref="RoomHub"/>
/// (CLAUDE.md §2.9 Server-emitted events). String constants are referenced
/// by both the server and the TS client wrapper in <c>packages/shared</c>.
///
/// Sprint 1 fires the subset of events the implemented flows actually
/// produce: <see cref="OpponentJoined"/>, <see cref="MatchStarted"/>,
/// <see cref="MoveAccepted"/>, <see cref="MatchEnded"/>, and
/// <see cref="OpponentDisconnected"/>. Other events (ClockTick,
/// OpponentReconnected, OpponentAbandoned, rematch events, RoomExpired)
/// arrive in later sprints.
/// </summary>
public static class RoomHubEvents
{
    public const string OpponentJoined = "OpponentJoined";
    public const string MatchStarted = "MatchStarted";
    public const string MoveAccepted = "MoveAccepted";
    public const string MatchEnded = "MatchEnded";
    public const string OpponentDisconnected = "OpponentDisconnected";
}
