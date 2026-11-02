using PlayMe.Domain.Platform;

namespace PlayMe.Application.Abstractions;

/// <summary>
/// Non-cryptographic randomness for fairness decisions (e.g. picking the
/// host's side under <c>SideSelectionMode.Random</c>). Backed by
/// <c>Random.Shared</c> in Infrastructure; substituted in tests for
/// determinism. Extends the Domain-level <see cref="IRandomSource"/> so
/// the same injected instance is handed straight to game modules'
/// <see cref="IBotPlayer"/> / <see cref="IBotSetup"/> (Sprint 12 seam D).
/// Security-sensitive randomness (room codes, player IDs) goes through
/// <see cref="IRoomCodeGenerator"/> / <see cref="IPlayerIdGenerator"/>
/// which use a cryptographic RNG.
/// </summary>
public interface IRandom : IRandomSource
{
}
