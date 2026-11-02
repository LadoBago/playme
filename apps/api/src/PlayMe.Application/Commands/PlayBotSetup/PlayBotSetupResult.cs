using PlayMe.Application.Dtos;

namespace PlayMe.Application.Commands.PlayBotSetup;

/// <summary>
/// Outcome of a bot setup dispatch. <see cref="Applied"/> is false when the
/// entry was stale (the bot had already committed, or the room left setup)
/// and nothing changed. When true, the sweeper broadcasts
/// <c>OpponentSetupCommitted</c> to the host, or <c>MatchStarted</c> when
/// <see cref="MatchStarted"/> — this commit completed the setup phase.
/// </summary>
public sealed record PlayBotSetupResult(
    RoomDto Room,
    bool Applied,
    bool MatchStarted);
