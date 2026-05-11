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

    public override string ToString() => Value;
}
