using PlayMe.Domain.Platform;

namespace PlayMe.Application.Abstractions;

/// <summary>
/// Source of new <see cref="RoomCode"/> values. Implementation MUST use a
/// cryptographic RNG (≥128 bits, URL-safe encoding) per CLAUDE.md §5.4 —
/// never <c>Guid.NewGuid</c>, never time-derived. The interface stays in
/// Application; the concrete generator lives in Infrastructure.
/// </summary>
public interface IRoomCodeGenerator
{
    RoomCode NewCode();
}
