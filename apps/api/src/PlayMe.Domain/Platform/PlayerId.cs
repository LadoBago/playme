namespace PlayMe.Domain.Platform;

/// <summary>
/// Crypto-random 128-bit opaque player identifier (CLAUDE.md §2.7, §5.4).
/// Generated server-side at room creation (host) and challenger registration;
/// stored both in the signed session token and in the room state, and the
/// two MUST match on every authorization check.
/// </summary>
public readonly record struct PlayerId
{
    public string Value { get; }

    public PlayerId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("PlayerId must be non-empty.", nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;
}
