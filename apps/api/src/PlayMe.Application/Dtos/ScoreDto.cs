namespace PlayMe.Application.Dtos;

/// <summary>
/// Wire view of the room's session-only series scoreboard
/// (docs/platform.md §1 #13). Counts roll up across rematches
/// inside the same room and reset only when the room itself dies.
/// </summary>
/// <param name="Host">Win count for the room's <c>Host</c> role.</param>
/// <param name="Challenger">Win count for the room's <c>Challenger</c> role.</param>
/// <param name="Draws">Drawn matches — shared, not per-player; tracked
/// for display context, not scoring.</param>
public sealed record ScoreDto(int Host, int Challenger, int Draws);
