namespace PlayMe.Domain.Platform;

/// <summary>
/// Two-role model from CLAUDE.md §2.3 #2 — every room has exactly one host
/// (the player who created it) and one challenger (the player who joined via
/// the invite link).
/// </summary>
public enum Role
{
    Host,
    Challenger,
}
