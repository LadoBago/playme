namespace PlayMe.Domain.Platform;

/// <summary>
/// Match ended by the server-side reconnect-grace hard cutoff
/// (docs/platform-and-games.md §1 #7). The disconnected side loses;
/// the opponent wins by adjudication, not by claim.
/// </summary>
public sealed record Disconnect(string LosingSide) : Outcome;
