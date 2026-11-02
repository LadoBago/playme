using System.Text.Json;

namespace PlayMe.Domain.Platform;

/// <summary>
/// Companion to <see cref="IBotPlayer"/> for modules that also implement
/// <see cref="ISetupGame"/> (Sprint 12 seam D; see docs/bots.md). A setup
/// game is bot-capable only when its module implements <em>both</em>
/// <see cref="IBotPlayer"/> and this interface; the platform checks the
/// pair by capability, never by <see cref="GameId"/>. Setup-less modules
/// never implement this.
///
/// <para>
/// The platform calls <see cref="ChooseSetup"/> once per match for the bot
/// side when the room enters <see cref="RoomStatus.SettingUp"/> (and again
/// on every rematch, which re-enters setup with fresh state), then commits
/// the result through the same <see cref="ISetupGame.ValidateSetup"/> /
/// <see cref="ISetupGame.ApplySetup"/> path a human commit takes. The
/// returned payload must therefore validate; a rejected return is a module
/// bug. Like <see cref="IBotPlayer.ChooseMove"/> the call is pure apart
/// from <paramref name="rng"/> and runs outside the room lock.
/// </para>
/// </summary>
public interface IBotSetup
{
    /// <summary>
    /// Produce the bot's one-and-final setup payload for
    /// <paramref name="side"/>. <paramref name="options"/> is the room's
    /// validated <see cref="Room.GameOptions"/> blob, passed through
    /// opaquely exactly as for <see cref="IGameModule.NewMatch"/>. The
    /// payload shape is the module's own <see cref="GameMove"/> subtype —
    /// the same one a human's <c>SubmitSetup</c> parses into.
    /// </summary>
    GameMove ChooseSetup(JsonElement? options, string side, BotLevel level, IRandomSource rng);
}
