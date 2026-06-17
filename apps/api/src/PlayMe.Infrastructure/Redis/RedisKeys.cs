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
    /// Sorted set: score = unix-ms deadline, value = <c>{roomCode}:{role}</c>.
    /// Post-match reconnect grace for disconnects in
    /// <see cref="PlayMe.Domain.Platform.RoomStatus.Ended"/> /
    /// <see cref="PlayMe.Domain.Platform.RoomStatus.AwaitingRematch"/>
    /// (state.md §2.4): a brief window covers refresh / locale toggle /
    /// transient blips. On expiry the sweeper transitions the room to
    /// <see cref="PlayMe.Domain.Platform.RoomStatus.Closed"/> and emits
    /// <c>OpponentExited</c>. Reuses <see cref="Scheduling.GraceMemberKey"/>
    /// since the encoding (room code, role) is identical to the
    /// in-progress grace.
    /// </summary>
    public const string PostMatchExit = $"{Prefix}postmatch_exit";

    /// <summary>
    /// Sorted set: score = unix-ms deadline, value =
    /// <c>{roomCode}|{gameId}</c>. One entry per room — enrolled at
    /// creation (deadline = creation + <c>RoomLifetimes.WaitingForOpponent</c>),
    /// ZREM'd when the match actually starts. The sweeper fires
    /// <c>room_expired</c> for any entry whose room is still
    /// <c>WaitingForOpponent</c> (or already reaped) at the deadline.
    /// gameId rides on the member because by then the room's own Redis
    /// key has elapsed and the handler can't load the room to learn it.
    /// </summary>
    public const string Expires = $"{Prefix}expires";

    /// <summary>
    /// Sorted set: score = unix-ms deadline, value = roomCode (Sprint 10
    /// seam C). One entry per room in
    /// <see cref="PlayMe.Domain.Platform.RoomStatus.SettingUp"/> — enrolled
    /// at SettingUp entry (deadline = entry + <c>ISetupGame.SetupBudget</c>),
    /// ZREM'd when setup completes or the match ends during setup. On fire
    /// the sweeper expires the room (terminal <c>Expired</c>) regardless of
    /// who committed — setup expiry never awards a win.
    /// </summary>
    public const string SetupDeadlines = $"{Prefix}setup_deadlines";

    /// <summary>
    /// Per-session rate-limit sliding-window sorted set
    /// (docs/security.md §5). One key per policy × subject; each member
    /// is a single recent acquisition timestamp.
    /// </summary>
    public static string Rate(string policy, string subject) =>
        $"{Prefix}rate:{policy}:session:{subject}";
}
