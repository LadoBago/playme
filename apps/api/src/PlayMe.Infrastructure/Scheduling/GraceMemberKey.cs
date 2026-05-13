using PlayMe.Domain.Platform;

// GraceMemberKey is used from files that also pull in StackExchange.Redis,
// which exposes its own `Role` enum. Centralise the alias here so callers
// don't have to repeat it at each using site.
namespace PlayMe.Infrastructure.Scheduling;

/// <summary>
/// Encoding for the composite sorted-set member in <c>playme:grace</c>:
/// <c>{roomCode}:{role}</c>. Centralised so the scheduler and the sweeper
/// agree on the format (and the sweeper can parse what the scheduler
/// writes).
/// </summary>
public static class GraceMemberKey
{
    public static string Encode(RoomCode code, Role role) =>
        $"{code.Value}:{role}";

    public static bool TryDecode(string member, out string roomCode, out Role role)
    {
        var separator = member.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0 || separator == member.Length - 1)
        {
            roomCode = string.Empty;
            role = default;
            return false;
        }

        roomCode = member[..separator];
        return Enum.TryParse(member[(separator + 1)..], ignoreCase: false, out role);
    }
}
