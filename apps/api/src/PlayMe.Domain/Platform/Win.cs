namespace PlayMe.Domain.Platform;

/// <summary>One side won.</summary>
public sealed record Win(string WinningSide) : Outcome;
