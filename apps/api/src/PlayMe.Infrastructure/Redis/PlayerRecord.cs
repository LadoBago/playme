using PlayMe.Domain.Platform;

namespace PlayMe.Infrastructure.Redis;

/// <summary>
/// JSON-friendly persisted shape of <see cref="Player"/>. Internal so it
/// stays a serialization-only concern.
/// </summary>
internal sealed record PlayerRecord(
    PlayerId Id,
    DisplayName DisplayName,
    string? Side,
    // Sprint 12 seam D. Defaults to Human so rooms persisted before the
    // field landed rehydrate unchanged.
    PlayerKind Kind = PlayerKind.Human);
