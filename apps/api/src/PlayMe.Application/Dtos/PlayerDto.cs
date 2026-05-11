namespace PlayMe.Application.Dtos;

/// <summary>
/// Public view of a player. Notably omits <c>PlayerId</c> — it's a server-
/// only secret used as the second auth factor (CLAUDE.md §5.4) and rides
/// only in the signed session cookie; never returned to clients.
/// </summary>
/// <param name="DisplayName">Sanitized name as displayed in the UI.</param>
/// <param name="Side">Side identifier ("x"/"o", "red"/"yellow", ...) once
/// resolved. Null only while the room is in <c>WaitingForOpponent</c> under
/// <c>SideSelectionMode.ChallengerPicks</c>.</param>
public sealed record PlayerDto(string DisplayName, string? Side);
