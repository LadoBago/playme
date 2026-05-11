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
}
