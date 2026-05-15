namespace PlayMe.Infrastructure.Redis;

/// <summary>
/// Redis key patterns (CLAUDE.md §2.8). All keys are <c>playme:</c>-prefixed
/// so the app's namespace is separated from anything else on the cluster.
/// Channel separator is <c>:</c> — Redis tooling renders it as a tree.
/// </summary>
internal static class RedisKeys
{
    public const string Prefix = "playme:";

    public static string Room(string code) => $"{Prefix}room:{code}";
    public static string RoomLock(string code) => $"{Prefix}room:{code}:lock";

    /// <summary>
    /// Sorted set: score = unix-ms deadline, value = roomCode. state.md §2.2:
    /// one scheduled timeout check per active room; the sweeper drains via
    /// <c>ZRANGEBYSCORE … LIMIT 0 N</c>, processes under the room lock, and
    /// <c>ZREM</c>s the entry whether or not the timeout was adjudicated.
    /// </summary>
    public const string Timeouts = $"{Prefix}timeouts";

    /// <summary>
    /// Sorted set: score = unix-ms deadline, value = <c>{roomCode}:{role}</c>.
    /// Mirrors the timeout schedule but keyed per (room, player) so each
    /// disconnect can have its own pending grace entry.
    /// </summary>
    public const string Grace = $"{Prefix}grace";

    /// <summary>
    /// Per-session rate-limit sliding-window sorted set
    /// (docs/security.md §5). One key per policy × subject; each member
    /// is a single recent acquisition timestamp.
    /// </summary>
    public static string Rate(string policy, string subject) =>
        $"{Prefix}rate:{policy}:session:{subject}";
}
