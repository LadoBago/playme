using PlayMe.Domain.Platform;

namespace PlayMe.Application.Abstractions;

/// <summary>
/// Source of new <see cref="PlayerId"/> values. Same primitive as
/// <see cref="IRoomCodeGenerator"/> (cryptographic RNG, ≥128 bits — CLAUDE.md
/// §5.4) but a distinct port so test doubles can substitute independently.
/// </summary>
public interface IPlayerIdGenerator
{
    PlayerId NewPlayerId();
}
