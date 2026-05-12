namespace PlayMe.Domain.Platform;

/// <summary>
/// Marker for a per-game match-state snapshot. Implementations are immutable
/// and self-describing (each game owns its board representation per CLAUDE.md
/// §2.3) — the platform never inspects state internals.
/// </summary>
public interface IGameState
{
}
