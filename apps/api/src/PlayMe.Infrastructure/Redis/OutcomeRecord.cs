namespace PlayMe.Infrastructure.Redis;

/// <summary>
/// Flat persisted shape of <see cref="Domain.Platform.Outcome"/>.
/// <see cref="Kind"/> is the discriminator ("win" / "draw" / "resign" /
/// "timeout"; "disconnect" arrives with Sprint 5). Mirrors <c>OutcomeDto</c>
/// but kept as a separate type so the storage format can evolve
/// independently of the wire DTO.
/// </summary>
internal sealed record OutcomeRecord(
    string Kind,
    string? WinningSide,
    string? ResigningSide,
    string? TimedOutSide);
