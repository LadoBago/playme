using PlayMe.Application.Abstractions;
using PlayMe.Application.Errors;
using PlayMe.Application.RateLimiting;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.SendEmote;

/// <summary>
/// Validates and authorizes an in-match emote, then tells the hub whether to
/// broadcast it. Deliberately lightweight versus the move/rematch handlers:
/// an emote mutates nothing, so there is no room lock and no
/// <c>SaveAsync</c> — the room is loaded read-only only to gate on status
/// and re-check membership.
///
/// <para>
/// Failure shape:
/// <list type="bullet">
///   <item>Unknown emote id → <see cref="PlatformErrors.EmoteUnknown"/>
///   (a contract violation; the hub turns it into a HubException).</item>
///   <item>Caller is not the stored player for the role →
///   <see cref="PlatformErrors.SessionUnauthorized"/>.</item>
///   <item>Rate-limited, room gone, or a status where emotes aren't shown
///   → <see cref="SendEmoteEffect.Suppressed"/> (a clean no-op, never an
///   error the sender sees).</item>
/// </list>
/// </para>
/// </summary>
public sealed class SendEmoteHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IRateLimiter _rateLimiter;

    public SendEmoteHandler(IRoomRepository rooms, IRateLimiter rateLimiter)
    {
        _rooms = rooms;
        _rateLimiter = rateLimiter;
    }

    public async Task<AppResult<SendEmoteHandlerResult>> HandleAsync(
        SendEmoteCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        // Contract violation (or a tampered client) — surface it rather than
        // dropping silently, and before spending any rate budget or a Redis
        // round-trip.
        if (!Emote.IsValid(cmd.EmoteId))
        {
            return AppResult<SendEmoteHandlerResult>.Fail(PlatformErrors.EmoteUnknown);
        }

        if (!RoomCode.TryCreate(cmd.RoomCode, out var code))
        {
            return Suppressed();
        }

        if (!await _rateLimiter.TryAcquireAsync(
                SessionRateLimitPolicies.Emote, cmd.CallerPlayerId, ct))
        {
            return Suppressed();
        }

        var room = await _rooms.LoadAsync(code, ct);
        if (room is null)
        {
            return Suppressed();
        }

        var stored = room.PlayerFor(cmd.CallerRole);
        if (stored is null || stored.Id.Value != cmd.CallerPlayerId)
        {
            return AppResult<SendEmoteHandlerResult>.Fail(PlatformErrors.SessionUnauthorized);
        }

        // Allowed during active play and on the post-game / rematch screen.
        // Blocked before both players are present (WaitingForOpponent), during
        // the unclocked setup phase (SettingUp), and once the room is gone
        // (Closed / Expired). Out-of-phase sends are a benign race against a
        // state change, so they're suppressed, not errored.
        if (room.Status is not (RoomStatus.InProgress
            or RoomStatus.Ended
            or RoomStatus.AwaitingRematch))
        {
            return Suppressed();
        }

        return AppResult<SendEmoteHandlerResult>.Ok(
            new SendEmoteHandlerResult(SendEmoteEffect.Delivered));
    }

    private static AppResult<SendEmoteHandlerResult> Suppressed() =>
        AppResult<SendEmoteHandlerResult>.Ok(new SendEmoteHandlerResult(SendEmoteEffect.Suppressed));
}
