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

    public override string ToString() => Value;
}
