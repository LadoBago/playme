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

    /// <summary>
    /// Non-throwing factory for use at the user-input boundary (CLAUDE.md §6
    /// "No exceptions for control flow"). Returns false with <paramref name="id"/>
    /// set to <c>default</c> when the input is null, empty, or whitespace.
    /// </summary>
    public static bool TryCreate(string? value, out PlayerId id)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            id = default;
            return false;
        }
        id = new PlayerId(value);
        return true;
    }

    public override string ToString() => Value;
}
