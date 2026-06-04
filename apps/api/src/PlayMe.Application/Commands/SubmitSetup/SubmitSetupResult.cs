using PlayMe.Application.Dtos;

namespace PlayMe.Application.Commands.SubmitSetup;

/// <summary>
/// Result of an accepted setup commit. <paramref name="MatchStarted"/> is
/// true when this commit completed the setup phase — the room is now
/// <c>InProgress</c>, the clock is running, and the hub broadcasts
/// <c>MatchStarted</c>; otherwise it broadcasts
/// <c>OpponentSetupCommitted</c> to the other player.
/// </summary>
public sealed record SubmitSetupResult(
    RoomDto Room,
    bool MatchStarted);
