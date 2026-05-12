using PlayMe.Application.Abstractions;

namespace PlayMe.Infrastructure.Random;

/// <summary>
/// Default <see cref="IRandom"/> backed by <see cref="System.Random.Shared"/>.
/// Thread-safe (per BCL docs); not cryptographically secure — only used for
/// fairness decisions (e.g. random side picking), never for secrets.
/// </summary>
public sealed class SystemRandom : IRandom
{
    public int NextInt(int maxExclusive) => System.Random.Shared.Next(maxExclusive);
}
