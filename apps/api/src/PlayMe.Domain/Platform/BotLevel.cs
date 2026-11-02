namespace PlayMe.Domain.Platform;

/// <summary>
/// Strength the host picked for a bot opponent at room creation (Sprint 12
/// seam D; see docs/bots.md). Platform-owned because it is a cross-game UI
/// concept — the create form offers the same three choices for every game
/// that supports a bot — not game vocabulary. What each level <em>means</em>
/// (search depth, heuristic, randomness) is decided by each module's
/// <see cref="IBotPlayer"/>; the only platform-wide promise is monotonic
/// strength: <see cref="Hard"/> is at least as strong as <see cref="Medium"/>,
/// which is at least as strong as <see cref="Easy"/>.
/// </summary>
public enum BotLevel
{
    Easy,
    Medium,
    Hard,
}
