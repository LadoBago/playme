using PlayMe.Application.Dtos;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.OfferRematch;

/// <summary>
/// Result of an accepted <c>OfferRematch</c>. <see cref="Effect"/> discriminates
/// the offer-recorded path from the implicit-accept path that fires when two
/// players' offers race under the room lock (docs/platform.md §1 #10).
/// The hub broadcasts <c>RematchOffered</c> for <see cref="RematchOfferResult.OfferRecorded"/>
/// and <c>MatchStarted</c> for <see cref="RematchOfferResult.ImplicitlyAccepted"/>.
/// </summary>
public sealed record OfferRematchHandlerResult(RoomDto Room, RematchOfferResult Effect);
