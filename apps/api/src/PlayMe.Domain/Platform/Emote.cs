namespace PlayMe.Domain.Platform;

/// <summary>
/// The fixed catalog of in-match emotes — a platform capability, not game
/// state (CLAUDE.md §7 "Platform thinness"). An emote is an ephemeral
/// player-to-player signal: validated against this allowlist, broadcast to
/// the opponent, and never persisted or replayed. The set is a closed
/// agreement with the web renderer; the ids are semantic ("smile", not an
/// emoji codepoint) so the client owns the glyph. Keep in sync with the Zod
/// allowlist in <c>packages/shared/src/realtime/emotes.ts</c>.
/// </summary>
public static class Emote
{
    /// <summary>Canonical, ordinal-compared set of valid emote ids.</summary>
    public static readonly IReadOnlySet<string> Ids = new HashSet<string>(StringComparer.Ordinal)
    {
        "smile",
        "like",
        "heart",
        "clap",
        "poke",
        "cry",
    };

    /// <summary>True if <paramref name="id"/> is a known emote id.</summary>
    public static bool IsValid(string id) => Ids.Contains(id);
}
