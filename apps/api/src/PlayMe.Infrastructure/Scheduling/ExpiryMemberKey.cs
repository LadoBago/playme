using PlayMe.Domain.Platform;

namespace PlayMe.Infrastructure.Scheduling;

/// <summary>
/// Encoding for the composite sorted-set member in <c>playme:expires</c>:
/// <c>{roomCode}|{gameId}</c>. <see cref="GraceMemberKey"/> uses
/// <c>:</c> for its room-and-role pair; we use <c>|</c> here because
/// gameIds are slugs that may contain <c>-</c> but never <c>|</c>, and
/// keeping the two encodings visually distinct prevents accidental
/// cross-parsing in code that touches both schedulers.
///
/// Centralised so the scheduler and the sweeper agree on the format,
/// and the sweeper can reconstruct both fields from a single ZRANGE
/// response without a side lookup.
/// </summary>
public static class ExpiryMemberKey
{
    public const char Separator = '|';

    public static string Encode(RoomCode code, GameId gameId) =>
        $"{code.Value}{Separator}{gameId.Value}";

    public static bool TryDecode(string member, out string roomCode, out string gameId)
    {
        var separator = member.IndexOf(Separator);
        if (separator <= 0 || separator == member.Length - 1)
        {
            roomCode = string.Empty;
            gameId = string.Empty;
            return false;
        }

        roomCode = member[..separator];
        gameId = member[(separator + 1)..];
        return true;
    }
}
