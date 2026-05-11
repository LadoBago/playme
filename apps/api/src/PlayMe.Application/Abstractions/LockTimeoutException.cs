namespace PlayMe.Application.Abstractions;

/// <summary>
/// Thrown by <see cref="IRoomRepository.WithLockAsync"/> when the Redis
/// distributed lock can't be acquired within the configured budget
/// (CLAUDE.md §2.8: ~500 ms). Handlers translate this to
/// <c>ErrorCode.RoomBusy</c>; clients are expected to retry.
/// </summary>
public sealed class LockTimeoutException : Exception
{
    public string RoomCode { get; }

    public LockTimeoutException(string roomCode)
        : base($"Could not acquire room lock for '{roomCode}' within the budget.")
    {
        RoomCode = roomCode;
    }
}
