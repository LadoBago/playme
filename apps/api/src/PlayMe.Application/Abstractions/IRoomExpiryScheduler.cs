using PlayMe.Domain.Platform;

namespace PlayMe.Application.Abstractions;

/// <summary>
/// Schedule the future <see cref="RoomStatus.WaitingForOpponent"/>
/// expiry check. Mirrors <see cref="ITimeoutScheduler"/> and
/// <see cref="IDisconnectGraceScheduler"/> in shape but carries the
/// <see cref="GameId"/> alongside the room code — by the time the
/// sweeper fires, the room's own Redis key has already expired, so the
/// sweeper can no longer load the room to learn its game. Encoding the
/// gameId in the scheduled member is the simplest way to keep
/// <c>room_expired</c> analytics populated without a second metadata
/// key with its own TTL coupling.
///
/// Implementation: <c>RedisRoomExpiryScheduler</c> in Infrastructure.
/// Keyed by <c>roomCode</c>; one outstanding entry per room.
/// </summary>
public interface IRoomExpiryScheduler
{
    /// <summary>
    /// Schedule (or replace) the expiry check for
    /// <paramref name="code"/> at <paramref name="deadline"/>. Called
    /// from <c>CreateRoomHandler</c> after the room is persisted with
    /// a deadline of <c>now + RoomLifetimes.WaitingForOpponent</c>.
    /// </summary>
    Task ScheduleAsync(
        RoomCode code,
        GameId gameId,
        DateTimeOffset deadline,
        CancellationToken ct);

    /// <summary>
    /// Cancel the pending entry for the given room. Called when the
    /// room transitions out of <see cref="RoomStatus.WaitingForOpponent"/>
    /// (i.e. a match has actually started). The caller passes
    /// <paramref name="gameId"/> alongside the code because the
    /// sorted-set member encodes both — the room is loaded under the
    /// lock at the cancel site, so the gameId is already in hand.
    /// Idempotent.
    /// </summary>
    Task CancelAsync(RoomCode code, GameId gameId, CancellationToken ct);
}
