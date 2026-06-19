namespace PlayMe.Api.Hubs;

/// <summary>
/// Names of the server → client events broadcast from <see cref="RoomHub"/>
/// (CLAUDE.md §2.9 Server-emitted events). String constants are referenced
/// by both the server and the TS client wrapper in <c>packages/shared</c>.
/// </summary>
public static class RoomHubEvents
{
    public const string OpponentJoined = "OpponentJoined";
    public const string MatchStarted = "MatchStarted";
    public const string SetupStarted = "SetupStarted";
    public const string OpponentSetupCommitted = "OpponentSetupCommitted";
    public const string MoveAccepted = "MoveAccepted";
    public const string MatchEnded = "MatchEnded";
    public const string ClockTick = "ClockTick";
    public const string OpponentDisconnected = "OpponentDisconnected";
    public const string OpponentReconnected = "OpponentReconnected";
    public const string OpponentExited = "OpponentExited";
    public const string RematchOffered = "RematchOffered";
    public const string RematchDeclined = "RematchDeclined";
    public const string RoomExpired = "RoomExpired";

    /// <summary>
    /// In-match emote reaction relayed to the opponent. Unlike every other
    /// event here it carries no room state — just the sender's role and the
    /// emote id — because an emote mutates nothing (CLAUDE.md §7).
    /// </summary>
    public const string EmoteReceived = "EmoteReceived";
}
