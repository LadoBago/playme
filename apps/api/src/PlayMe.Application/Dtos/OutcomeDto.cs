namespace PlayMe.Application.Dtos;

/// <summary>
/// Flat wire shape for <see cref="Domain.Platform.Outcome"/>. <see cref="Kind"/>
/// is the discriminator ("win" / "draw" / "resign" / "timeout"; "disconnect"
/// arrives with Sprint 5). Per-game "how" details — winning-line cells, mating
/// piece, etc. — live inside the game's own state blob (<c>MatchDto.State</c>)
/// per CLAUDE.md §7 "Platform thinness".
/// </summary>
public sealed record OutcomeDto(
    string Kind,
    string? WinningSide,
    string? ResigningSide,
    string? TimedOutSide);
