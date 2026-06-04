namespace PlayMe.Domain.Platform;

/// <summary>
/// Outcome of a successful <see cref="Room.TryOfferRematch"/> call.
/// Discriminates the "first offer recorded" case from the implicit-accept
/// that fires when two players' offers race under the room lock
/// (docs/platform.md §1 #10). The hub broadcasts different events
/// in each case: <c>RematchOffered</c> for <see cref="OfferRecorded"/>,
/// <c>MatchStarted</c> for <see cref="ImplicitlyAccepted"/>.
/// </summary>
public enum RematchOfferResult
{
    OfferRecorded,
    ImplicitlyAccepted,
}
