using PlayMe.Application.Abstractions;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.AdjudicateTimeout;

/// <summary>
/// Adjudicate a scheduled timeout. The Infrastructure sweeper
/// (state.md §2.2) has already acquired the per-room distributed lock
/// before calling this handler — so we do <em>not</em> wrap the read/write
/// in <c>IRoomRepository.WithLockAsync</c> again (that would deadlock).
///
/// The handler is idempotent: if a move landed since the entry was
/// scheduled, the deadline has advanced and <c>HasActivePlayerTimedOut</c>
/// returns false — we drop silently. The sweeper <c>ZREM</c>s the entry
/// either way.
/// </summary>
public sealed class AdjudicateTimeoutHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IClock _clock;
    private readonly IClockService _clockService;

    public AdjudicateTimeoutHandler(
        IRoomRepository rooms,
        IClock clock,
        IClockService clockService)
    {
        _rooms = rooms;
        _clock = clock;
        _clockService = clockService;
    }

    public async Task<AppResult<AdjudicateTimeoutResult>> HandleAsync(
        AdjudicateTimeoutCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        RoomCode code;
        try { code = new RoomCode(cmd.RoomCode); }
        catch (ArgumentException)
        {
            return AppResult<AdjudicateTimeoutResult>.Fail(PlatformErrors.RoomNotFound);
        }

        var room = await _rooms.LoadAsync(code, ct);
        if (room is null)
        {
            return AppResult<AdjudicateTimeoutResult>.Ok(
                new AdjudicateTimeoutResult(Room: null, TimedOut: false));
        }

        var now = _clock.UtcNow;

        if (room.Status != RoomStatus.InProgress
            || room.CurrentMatch is null
            || room.CurrentMatch.IsEnded)
        {
            return AppResult<AdjudicateTimeoutResult>.Ok(
                new AdjudicateTimeoutResult(RoomMapper.ToDto(room, now), TimedOut: false));
        }

        var match = room.CurrentMatch;
        if (!_clockService.HasActivePlayerTimedOut(match.Clock, now))
        {
            return AppResult<AdjudicateTimeoutResult>.Ok(
                new AdjudicateTimeoutResult(RoomMapper.ToDto(room, now), TimedOut: false));
        }

        match.ApplyTimeout(match.SideToMove, now);
        room.EndCurrentMatch();
        await _rooms.SaveAsync(room, ct);

        return AppResult<AdjudicateTimeoutResult>.Ok(
            new AdjudicateTimeoutResult(RoomMapper.ToDto(room, now), TimedOut: true));
    }
}
