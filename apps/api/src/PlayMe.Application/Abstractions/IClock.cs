namespace PlayMe.Application.Abstractions;

/// <summary>
/// Time source. CLAUDE.md §2.4: Domain and Application never call
/// DateTime.UtcNow directly — they take <see cref="IClock"/> as a dependency
/// so logic that depends on time is testable.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
