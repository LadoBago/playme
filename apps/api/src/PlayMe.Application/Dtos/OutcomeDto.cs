using PlayMe.Domain.Platform;

namespace PlayMe.Application.Dtos;

/// <summary>
/// Flat wire shape for <see cref="Outcome"/>. <see cref="Kind"/> is the
/// discriminator ("win" / "draw" / "resign" in Sprint 1; later sprints add
/// "timeout" and "disconnect"). Only the fields relevant to that kind are
/// populated.
/// </summary>
public sealed record OutcomeDto(
    string Kind,
    string? WinningSide,
    string? ResigningSide,
    IReadOnlyList<BoardCoordinate>? WinningLine);
