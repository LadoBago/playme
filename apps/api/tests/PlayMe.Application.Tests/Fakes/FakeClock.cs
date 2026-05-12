using PlayMe.Application.Abstractions;

namespace PlayMe.Application.Tests.Fakes;

/// <summary>Mutable clock for handler tests. Lets a test pin "now" deterministically.</summary>
public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } =
        new(2026, 5, 12, 12, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan delta) => UtcNow += delta;
}
