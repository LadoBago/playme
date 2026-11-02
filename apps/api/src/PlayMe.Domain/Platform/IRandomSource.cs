namespace PlayMe.Domain.Platform;

/// <summary>
/// Non-cryptographic randomness handed to game modules that need it
/// (today: tie-breaking and setup placement in <see cref="IBotPlayer"/> /
/// <see cref="IBotSetup"/>). Lives in Domain because Domain references
/// nothing; the Application-layer <c>IRandom</c> port extends this
/// interface so the platform passes its single injected instance straight
/// through, and tests substitute a seeded implementation for determinism.
/// Never use this for anything that must be unguessable — room codes and
/// player IDs go through the cryptographic generators in Infrastructure.
/// </summary>
public interface IRandomSource
{
    /// <summary>A non-negative integer less than <paramref name="maxExclusive"/>.</summary>
    int NextInt(int maxExclusive);
}
