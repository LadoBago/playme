namespace PlayMe.Application.Bots;

/// <summary>
/// What the bot in a room owes right now, as decided by
/// <see cref="BotTurnPolicy.PendingAction"/> (Sprint 12 seam D).
/// </summary>
public enum BotAction
{
    /// <summary>Nothing: not a bot room, no live match, or the human is on the move.</summary>
    None,

    /// <summary>The room is in setup and the bot has not committed its setup yet.</summary>
    Setup,

    /// <summary>The match is in progress and the bot's side is to move.</summary>
    Move,
}
