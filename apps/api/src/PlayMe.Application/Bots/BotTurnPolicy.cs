using PlayMe.Domain.Platform;

namespace PlayMe.Application.Bots;

/// <summary>
/// The one place that decides whether a bot owes an action (Sprint 12
/// seam D). Pure: reads the room aggregate, returns a <see cref="BotAction"/>.
/// Both the scheduling side (<see cref="BotTurnPlanner"/>, after every
/// state change) and the executing side (<c>PlayBotMoveHandler</c> /
/// <c>PlayBotSetupHandler</c>, at fire time and again under the lock) ask
/// this same question, so a stale scheduled entry can never make the bot
/// act out of turn. The bot is always the challenger
/// (<see cref="Room.CreateWithBot"/>).
/// </summary>
public static class BotTurnPolicy
{
    public static BotAction PendingAction(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);

        if (room.Challenger is not { IsBot: true } bot || room.CurrentMatch is not { IsEnded: false } match)
        {
            return BotAction.None;
        }

        return room.Status switch
        {
            RoomStatus.SettingUp when !match.ChallengerSetupCommitted => BotAction.Setup,
            RoomStatus.InProgress when match.SideToMove == bot.Side => BotAction.Move,
            _ => BotAction.None,
        };
    }
}
