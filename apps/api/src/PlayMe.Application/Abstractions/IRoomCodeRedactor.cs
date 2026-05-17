namespace PlayMe.Application.Abstractions;

/// <summary>
/// Produces a deterministic, correlation-safe identifier for a room code
/// suitable for log fields. Room codes are 128-bit unguessable invite
/// tokens (effectively secrets) per docs/security.md §8 and must never
/// appear in logs at <c>Information</c> or above; handlers and adapters
/// inject this port and emit the redacted form instead. The concrete
/// implementation lives in <c>PlayMe.Infrastructure/Security/</c>.
/// </summary>
public interface IRoomCodeRedactor
{
    /// <summary>
    /// Returns the log-safe identifier for <paramref name="code"/>. Same
    /// input always yields the same output so log lines on the same room
    /// still correlate.
    /// </summary>
    string Redact(string code);
}
