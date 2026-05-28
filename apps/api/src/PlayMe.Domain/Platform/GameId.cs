namespace PlayMe.Domain.Platform;

/// <summary>
/// Identifier of a game module (CLAUDE.md §2.7). The platform layer does not
/// enumerate valid IDs — each <see cref="IGameModule"/> declares its own,
/// keeping games self-contained per §2.3.
/// </summary>
public readonly record struct GameId
{
    public string Value { get; }

    public GameId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("GameId must be a non-empty slug.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Non-throwing factory for use at the user-input boundary (CLAUDE.md §6
    /// "No exceptions for control flow"). Returns false with <paramref name="id"/>
    /// set to <c>default</c> when the input is null, empty, or whitespace.
    /// </summary>
    public static bool TryCreate(string? value, out GameId id)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            id = default;
            return false;
        }
        id = new GameId(value);
        return true;
    }

    public override string ToString() => Value;
}
