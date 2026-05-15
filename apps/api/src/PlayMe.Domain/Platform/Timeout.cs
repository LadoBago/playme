namespace PlayMe.Domain.Platform;

/// <summary>One side's clock ran out. The opponent wins.</summary>
public sealed record Timeout(string TimedOutSide) : Outcome;
