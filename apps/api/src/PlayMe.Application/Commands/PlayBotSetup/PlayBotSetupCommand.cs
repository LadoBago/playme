namespace PlayMe.Application.Commands.PlayBotSetup;

/// <summary>
/// Dispatched by the bot-turn sweeper (Sprint 12 seam D) when a setup
/// game's bot owes its setup commit. No identity: the handler derives
/// everything from the room's bot seat. Never reachable from the Hub.
/// </summary>
public sealed record PlayBotSetupCommand(string RoomCode);
