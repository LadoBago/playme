namespace PlayMe.Application.Abstractions;

/// <summary>
/// Non-cryptographic randomness for fairness decisions (e.g. picking the
/// host's side under <c>SideSelectionMode.Random</c>). Backed by
/// <c>Random.Shared</c> in Infrastructure; substituted in tests for
/// determinism. Security-sensitive randomness (room codes, player IDs) goes
/// through <see cref="IRoomCodeGenerator"/> / <see cref="IPlayerIdGenerator"/>
/// which use a cryptographic RNG.
/// </summary>
public interface IRandom
{
    /// <summary>A non-negative integer less than <paramref name="maxExclusive"/>.</summary>
    int NextInt(int maxExclusive);
}
