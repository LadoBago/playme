using PlayMe.Domain.Platform;

namespace PlayMe.Infrastructure.Redis;

/// <summary>
/// JSON-friendly persisted shape of <see cref="Player"/>. Internal so it
/// stays a serialization-only concern.
/// </summary>
internal sealed record PlayerRecord(
    PlayerId Id,
    DisplayName DisplayName,
    string? Side);
