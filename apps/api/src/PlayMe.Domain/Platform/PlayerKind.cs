namespace PlayMe.Domain.Platform;

/// <summary>
/// Who occupies a seat (Sprint 12 seam D; docs/bots.md). A
/// <see cref="Bot"/> is a virtual challenger driven by the game module's
/// <see cref="IBotPlayer"/>: same role, same side rules, same clock as a
/// human, but it has no session, is always connected, and never has a
/// disconnect grace scheduled against it. The host seat is always
/// <see cref="Human"/>.
/// </summary>
public enum PlayerKind
{
    Human,
    Bot,
}
