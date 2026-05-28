namespace PlayMe.Domain.Platform;

/// <summary>
/// Opaque, high-entropy room identifier (CLAUDE.md §2.7, §5.4). Generated
/// by <c>IRoomCodeGenerator</c> in Infrastructure using a cryptographic RNG;
/// the value object only enforces that the string is non-empty.
/// </summary>
public readonly record struct RoomCode
{
    public string Value { get; }

    public RoomCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("RoomCode must be non-empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Non-throwing factory for use at the user-input boundary (CLAUDE.md §6
    /// "No exceptions for control flow"). Returns false with <paramref name="code"/>
    /// set to <c>default</c> when the input is null, empty, or whitespace.
    /// The throwing ctor remains for trusted inputs (e.g. Redis rehydration).
    /// </summary>
    public static bool TryCreate(string? value, out RoomCode code)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            code = default;
            return false;
        }
        code = new RoomCode(value);
        return true;
    }

    public override string ToString() => Value;
}
