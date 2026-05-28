using PlayMe.Application.Abstractions;
using PlayMe.Application.Errors;
using PlayMe.Application.Telemetry;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.AdjudicateRoomExpiry;

/// <summary>
/// Adjudicate a scheduled room expiry. The Infrastructure sweeper
/// (state.md §2.2) has already acquired the per-room distributed lock
/// before calling — so we do <em>not</em> wrap in
/// <c>IRoomRepository.WithLockAsync</c> again (that would deadlock,
/// mirroring <see cref="AdjudicateTimeout.AdjudicateTimeoutHandler"/>).
///
/// Idempotency / race handling:
/// - Room is null (already reaped by Redis TTL) → fire the event. This
///   is the dominant case: room TTL = expiry deadline, so by the time
///   we sweep the key has typically just elapsed.
/// - Room is <see cref="RoomStatus.WaitingForOpponent"/> → also fire.
///   This handles the (tiny) window where the sweeper acquires the
///   lock before the TTL fully elapses.
/// - Anything else (InProgress, Ended, AwaitingRematch, Closed) →
///   drop silently. A challenger joined between schedule and sweep;
///   the entry is stale.
/// </summary>
public sealed class AdjudicateRoomExpiryHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IAnalyticsClient _analytics;

    public AdjudicateRoomExpiryHandler(
        IRoomRepository rooms,
        IAnalyticsClient analytics)
    {
        _rooms = rooms;
        _analytics = analytics;
    }

    public async Task<AppResult<AdjudicateRoomExpiryResult>> HandleAsync(
        AdjudicateRoomExpiryCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (!RoomCode.TryCreate(cmd.RoomCode, out var code))
        {
            return AppResult<AdjudicateRoomExpiryResult>.Fail(PlatformErrors.RoomNotFound);
        }

        var room = await _rooms.LoadAsync(code, ct);
        var expired = room is null || room.Status == RoomStatus.WaitingForOpponent;

        if (!expired)
        {
            // Joined late — the room moved to InProgress (or past it)
            // between schedule and sweep. The schedule was cancelled
            // by RegisterPresenceHandler, but the sweeper's batch may
            // have already picked up the entry; treat as no-op.
            return AppResult<AdjudicateRoomExpiryResult>.Ok(
                new AdjudicateRoomExpiryResult(Expired: false));
        }

        await _analytics.TrackAsync(
            AnalyticsEvents.RoomExpired,
            code.Value,
            AnalyticsEvents.RoomExpiredProperties(cmd.GameId),
            ct);

        // SignalR broadcast is the sweeper's responsibility — same
        // split as the timeout/grace handlers (handler owns analytics
        // + state; sweeper owns the network fan-out via IRoomNotifier).
        return AppResult<AdjudicateRoomExpiryResult>.Ok(
            new AdjudicateRoomExpiryResult(Expired: true));
    }
}
