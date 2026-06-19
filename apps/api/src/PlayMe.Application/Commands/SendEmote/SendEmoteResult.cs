namespace PlayMe.Application.Commands.SendEmote;

/// <summary>
/// Outcome of an accepted <c>SendEmote</c> call. The hub only broadcasts
/// <c>EmoteReceived</c> for <see cref="SendEmoteEffect.Delivered"/>;
/// <see cref="SendEmoteEffect.Suppressed"/> is a benign no-op (rate-limited,
/// room gone, or a state where emotes aren't shown) that resolves cleanly
/// without surfacing an error to the sender.
/// </summary>
public enum SendEmoteEffect
{
    /// <summary>Validated and cleared for broadcast to the opponent.</summary>
    Delivered,

    /// <summary>Dropped silently — no broadcast, no error.</summary>
    Suppressed,
}

/// <summary>Result of a <c>SendEmote</c> command (see <see cref="SendEmoteEffect"/>).</summary>
public sealed record SendEmoteHandlerResult(SendEmoteEffect Effect);
