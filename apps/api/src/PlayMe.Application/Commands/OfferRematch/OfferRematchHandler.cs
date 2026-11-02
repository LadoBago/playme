using PlayMe.Application.Abstractions;
using PlayMe.Application.Bots;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Application.RateLimiting;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.OfferRematch;

/// <summary>
/// First step of the rematch handshake (docs/platform.md §1 #10).
/// Under the room lock:
/// <list type="bullet">
///   <item>From <c>Ended</c>: record the caller as the offerer and flip the
///   room to <c>AwaitingRematch</c>; hub emits <c>RematchOffered</c>.</item>
///   <item>From <c>AwaitingRematch</c> with the opposite caller: treat as
///   implicit accept, swap sides, start a new match; hub emits
///   <c>MatchStarted</c>.</item>
///   <item>Anything else (including a duplicate offer from the same caller)
///   → <c>RematchInvalidState</c>.</item>
///   <item>In a bot room (Sprint 12 seam D) the bot accepts on the spot:
///   the host's offer resolves as <see cref="RematchOfferResult.ImplicitlyAccepted"/>
///   in the same call, sides swap, a fresh match starts.</item>
/// </list>
/// </summary>
public sealed class OfferRematchHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IGameModuleRegistry _games;
    private readonly IClock _clock;
    private readonly ITimeoutScheduler _timeouts;
    private readonly ISetupDeadlineScheduler _setupDeadlines;
    private readonly IRateLimiter _rateLimiter;
    private readonly BotTurnPlanner _botTurns;

    public OfferRematchHandler(
        IRoomRepository rooms,
        IGameModuleRegistry games,
        IClock clock,
        ITimeoutScheduler timeouts,
        ISetupDeadlineScheduler setupDeadlines,
        IRateLimiter rateLimiter,
        BotTurnPlanner botTurns)
    {
        _rooms = rooms;
        _games = games;
        _clock = clock;
        _timeouts = timeouts;
        _setupDeadlines = setupDeadlines;
        _rateLimiter = rateLimiter;
        _botTurns = botTurns;
    }

    public async Task<AppResult<OfferRematchHandlerResult>> HandleAsync(
        OfferRematchCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (!RoomCode.TryCreate(cmd.RoomCode, out var code))
        {
            return AppResult<OfferRematchHandlerResult>.Fail(PlatformErrors.RoomNotFound);
        }

        if (!await _rateLimiter.TryAcquireAsync(
                SessionRateLimitPolicies.Rematch, cmd.CallerPlayerId, ct))
        {
            return AppResult<OfferRematchHandlerResult>.Fail(PlatformErrors.RateExceeded);
        }

        try
        {
            return await _rooms.WithLockAsync(code, async () =>
            {
                var room = await _rooms.LoadAsync(code, ct);
                if (room is null)
                {
                    return AppResult<OfferRematchHandlerResult>.Fail(PlatformErrors.RoomNotFound);
                }

                var stored = room.PlayerFor(cmd.CallerRole);
                if (stored is null || stored.Id.Value != cmd.CallerPlayerId)
                {
                    return AppResult<OfferRematchHandlerResult>.Fail(PlatformErrors.SessionUnauthorized);
                }

                var module = _games.GetModule(room.GameId);
                var now = _clock.UtcNow;

                if (!room.TryOfferRematch(cmd.CallerRole, module, now, out var effect))
                {
                    return AppResult<OfferRematchHandlerResult>.Fail(PlatformErrors.RematchInvalidState);
                }

                // The bot never declines (Sprint 12 seam D): accept the
                // host's offer right here so the caller sees one
                // MatchStarted instead of an offer that hangs.
                if (effect == RematchOfferResult.OfferRecorded && room.HasBotChallenger)
                {
                    room.AcceptRematch(Role.Challenger, module, now);
                    effect = RematchOfferResult.ImplicitlyAccepted;
                }

                await _rooms.SaveAsync(room, ct);

                // Implicit accept transitioned the room into a fresh match —
                // schedule the active player's timeout (or, for setup games,
                // the fresh setup-phase deadline; Sprint 10 seam C). The
                // OfferRecorded path keeps the room post-Ended; no clock is
                // running yet.
                if (effect == RematchOfferResult.ImplicitlyAccepted && room.CurrentMatch is not null)
                {
                    if (room.Status == RoomStatus.SettingUp && module is ISetupGame setupGame)
                    {
                        await _setupDeadlines.ScheduleAsync(code, now + setupGame.SetupBudget, ct);
                    }
                    else
                    {
                        await _timeouts.ScheduleAsync(
                            code,
                            room.CurrentMatch.Clock.ActivePlayerDeadline(),
                            ct);
                    }
                    await _botTurns.ScheduleIfPendingAsync(room, now, ct);
                }

                return AppResult<OfferRematchHandlerResult>.Ok(new OfferRematchHandlerResult(
                    Room: RoomMapper.ToDto(room, now, _games),
                    Effect: effect));
            }, ct);
        }
        catch (LockTimeoutException)
        {
            return AppResult<OfferRematchHandlerResult>.Fail(PlatformErrors.RoomBusy);
        }
    }
}
