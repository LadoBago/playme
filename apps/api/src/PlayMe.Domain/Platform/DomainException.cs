namespace PlayMe.Domain.Platform;

/// <summary>
/// Thrown when a domain invariant is violated — i.e. when a caller asks the
/// model to do something that should never have been reachable (per CLAUDE.md
/// §8 "throw domain exceptions for invariants; return result types for
/// expected failure paths"). Expected user-facing failures (illegal moves,
/// room not found) use <see cref="MoveResult"/> or Application-layer error
/// codes instead.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception inner) : base(message, inner) { }
}
