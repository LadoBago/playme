namespace PlayMe.Application.Commands.PlayBotMove;

/// <summary>
/// Dispatched by the bot-turn sweeper (Sprint 12 seam D) when a room's
/// scheduled bot action falls due. Carries no identity: the bot has no
/// session, and the handler derives everything from the room's own bot
/// seat. Never reachable from the Hub.
/// </summary>
public sealed record PlayBotMoveCommand(string RoomCode);
