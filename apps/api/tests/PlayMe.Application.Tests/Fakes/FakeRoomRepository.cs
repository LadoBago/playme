using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IRoomRepository"/> for handler tests. The lock
/// is just a single-thread <see cref="SemaphoreSlim"/> — sufficient to
/// exercise handler flow without real Redis.
/// </summary>
public sealed class FakeRoomRepository : IRoomRepository
{
    private readonly Dictionary<string, Room> _rooms = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public Task<Room?> LoadAsync(RoomCode code, CancellationToken ct) =>
        Task.FromResult(_rooms.GetValueOrDefault(code.Value));

    public Task SaveAsync(Room room, CancellationToken ct)
    {
        _rooms[room.Code.Value] = room;
        return Task.CompletedTask;
    }

    public Task<bool> CreateAsync(Room room, CancellationToken ct)
    {
        if (_rooms.ContainsKey(room.Code.Value)) return Task.FromResult(false);
        _rooms[room.Code.Value] = room;
        return Task.FromResult(true);
    }

    public Task<T> WithLockAsync<T>(
        RoomCode code,
        Func<Task<T>> work,
        CancellationToken ct) =>
        WithLockAsync(code, TimeSpan.FromSeconds(1), work, ct);

    public async Task<T> WithLockAsync<T>(
        RoomCode code,
        TimeSpan acquireWait,
        Func<Task<T>> work,
        CancellationToken ct)
    {
        if (!await _lock.WaitAsync(acquireWait, ct))
        {
            throw new LockTimeoutException(code.Value);
        }
        try { return await work(); }
        finally { _lock.Release(); }
    }

    public void Seed(Room room) => _rooms[room.Code.Value] = room;
}
