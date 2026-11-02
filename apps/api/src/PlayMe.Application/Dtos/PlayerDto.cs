using PlayMe.Domain.Platform;

namespace PlayMe.Application.Dtos;

/// <summary>
/// Public view of a player. Notably omits <c>PlayerId</c> — it's a server-
/// only secret used as the second auth factor (CLAUDE.md §5.4) and rides
/// only in the signed session cookie; never returned to clients.
/// </summary>
/// <param name="DisplayName">Sanitized name as displayed in the UI. For a
/// bot this is a placeholder — the web renders a bot from
/// <paramref name="Kind"/> through an i18n key, never from this string.</param>
/// <param name="Side">Side identifier ("x"/"o", "red"/"yellow", ...) once
/// resolved. Null only while the room is in <c>WaitingForOpponent</c> under
/// <c>SideSelectionMode.ChallengerPicks</c>.</param>
/// <param name="Kind">Who occupies the seat (Sprint 12 seam D). Serialized
/// as <c>"human"</c> / <c>"bot"</c>.</param>
public sealed record PlayerDto(
    string DisplayName,
    string? Side,
    PlayerKind Kind = PlayerKind.Human);
