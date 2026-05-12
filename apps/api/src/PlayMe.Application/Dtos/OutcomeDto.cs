using PlayMe.Domain.Platform;

namespace PlayMe.Application.Dtos;

/// <summary>
/// Flat wire shape for <see cref="Outcome"/>. <see cref="Kind"/> is the
/// discriminator ("win" / "draw" / "resign" / "timeout"; "disconnect"
/// arrives with Sprint 5). Only the fields relevant to that kind are
/// populated.
/// </summary>
public sealed record OutcomeDto(
    string Kind,
    string? WinningSide,
    string? ResigningSide,
    string? TimedOutSide,
    IReadOnlyList<BoardCoordinate>? WinningLine);
