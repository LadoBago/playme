namespace PlayMe.Domain.Platform;

/// <summary>A player resigned the match.</summary>
public sealed record Resign(string ResigningSide) : Outcome;
