using PlayMe.Domain.Platform;

namespace PlayMe.Application.Abstractions;

/// <summary>
/// Persistence boundary for <see cref="Room"/>. Implemented by
/// <c>RedisRoomRepository</c> in Infrastructure (CLAUDE.md §2.8) using a
/// single-JSON-blob layout and a per-room distributed lock for atomic
/// mutations.
/// </summary>
public interface IRoomRepository
{
    /// <summary>Load a room by code. Returns null if not present (or expired).</summary>
    Task<Room?> LoadAsync(RoomCode code, CancellationToken ct);

    /// <summary>
    /// Overwrite a room's persisted state and refresh its TTL according to
    /// its current <see cref="RoomStatus"/> (per §2.8 TTL table).
    /// </summary>
    Task SaveAsync(Room room, CancellationToken ct);

    /// <summary>
    /// Atomically insert a brand-new room. Returns false if a room with the
    /// same code already exists — handlers should regenerate the code and
    /// retry. Sets the initial TTL for <see cref="RoomStatus.WaitingForOpponent"/>.
    /// </summary>
    Task<bool> CreateAsync(Room room, CancellationToken ct);

    /// <summary>
    /// Execute <paramref name="work"/> while holding the per-room distributed
    /// lock (CLAUDE.md §2.8). Lock TTL ≈ 5 s; acquire budget ≈ 500 ms; on
    /// timeout, throws <see cref="LockTimeoutException"/> so handlers can
    /// translate to <c>ErrorCode.RoomBusy</c>. The lock is released on exit
    /// (success or exception).
    /// </summary>
    Task<T> WithLockAsync<T>(
        RoomCode code,
        Func<Task<T>> work,
        CancellationToken ct);
}
