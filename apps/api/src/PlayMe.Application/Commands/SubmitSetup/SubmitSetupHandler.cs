using PlayMe.Application.Abstractions;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Application.RateLimiting;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.SubmitSetup;

/// <summary>
/// The setup-commit pipeline (Sprint 10 seam C; docs/games/seabattle.md).
/// Inside the room's distributed lock: authorize the caller, verify the
/// room is in <see cref="RoomStatus.SettingUp"/>, reject a double commit
/// (one commit per side, final — a platform rule), then parse and validate
/// the payload via the game module, apply it, and — when the module
/// reports setup complete — transition the room to
/// <see cref="RoomStatus.InProgress"/>: clock re-stamped to now, first
/// no-move timeout scheduled, setup deadline cancelled.
///
/// Fully game-agnostic: payload shape, validation, and reject vocabulary
/// come from the module (CLAUDE.md §7 "Platform thinness"); the platform
/// owns only the lifecycle and the commit bookkeeping.
/// </summary>
public sealed class SubmitSetupHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IGameModuleRegistry _games;
    private readonly IClock _clock;
    private readonly ITimeoutScheduler _timeouts;
    private readonly ISetupDeadlineScheduler _setupDeadlines;
    private readonly IRateLimiter _rateLimiter;

    public SubmitSetupHandler(
        IRoomRepository rooms,
        IGameModuleRegistry games,
        IClock clock,
        ITimeoutScheduler timeouts,
        ISetupDeadlineScheduler setupDeadlines,
        IRateLimiter rateLimiter)
    {
        _rooms = rooms;
        _games = games;
        _clock = clock;
        _timeouts = timeouts;
        _setupDeadlines = setupDeadlines;
        _rateLimiter = rateLimiter;
    }

    public async Task<AppResult<SubmitSetupResult>> HandleAsync(
        SubmitSetupCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (!RoomCode.TryCreate(cmd.RoomCode, out var code))
        {
            return AppResult<SubmitSetupResult>.Fail(PlatformErrors.RoomNotFound);
        }

        if (!await _rateLimiter.TryAcquireAsync(
                SessionRateLimitPolicies.SubmitSetup, cmd.CallerPlayerId, ct))
        {
            return AppResult<SubmitSetupResult>.Fail(PlatformErrors.RateExceeded);
        }

        try
        {
            return await _rooms.WithLockAsync(code, async () =>
            {
                var room = await _rooms.LoadAsync(code, ct);
                if (room is null)
                {
                    return AppResult<SubmitSetupResult>.Fail(PlatformErrors.RoomNotFound);
                }

                var stored = room.PlayerFor(cmd.CallerRole);
                if (stored is null || stored.Id.Value != cmd.CallerPlayerId)
                {
                    return AppResult<SubmitSetupResult>.Fail(PlatformErrors.SessionUnauthorized);
                }

                if (room.Status != RoomStatus.SettingUp || room.CurrentMatch is null)
                {
                    return AppResult<SubmitSetupResult>.Fail(PlatformErrors.SetupNotInSetup);
                }

                var match = room.CurrentMatch;
                if (match.IsEnded)
                {
                    return AppResult<SubmitSetupResult>.Fail(PlatformErrors.SetupNotInSetup);
                }

                if (match.HasCommittedSetup(cmd.CallerRole))
                {
                    return AppResult<SubmitSetupResult>.Fail(PlatformErrors.SetupAlreadyCommitted);
                }

                var module = _games.GetModule(room.GameId);
                if (module is not ISetupGame setupGame)
                {
                    // Unreachable in practice — a room only enters SettingUp
                    // for ISetupGame modules — but the defensive check keeps
                    // a bad cast out of the pipeline if state ever drifts.
                    return AppResult<SubmitSetupResult>.Fail(PlatformErrors.SetupNotInSetup);
                }

                var callerSide = stored.Side
                    ?? throw new InvalidOperationException(
                        "Caller has no side resolved — TryStartMatch should have rejected this room.");

                var parser = _games.GetMoveParser(room.GameId);
                var parseResult = parser.Parse(cmd.Setup);
                if (!parseResult.Succeeded)
                {
                    return parseResult.ToFailure<SubmitSetupResult>();
                }

                var rejectKey = setupGame.ValidateSetup(match.State, callerSide, parseResult.Value!);
                if (rejectKey is not null)
                {
                    return AppResult<SubmitSetupResult>.Fail(rejectKey);
                }

                var newState = setupGame.ApplySetup(match.State, callerSide, parseResult.Value!);
                match.ApplySetup(newState, cmd.CallerRole);

                var now = _clock.UtcNow;
                var matchStarted = setupGame.IsSetupComplete(match.State);
                if (matchStarted)
                {
                    room.CompleteSetup(now);
                    await _rooms.SaveAsync(room, ct);
                    await _setupDeadlines.CancelAsync(code, ct);
                    // The first mover's clock starts now (docs/platform.md
                    // §1 #12) — schedule the first no-move timeout check.
                    await _timeouts.ScheduleAsync(
                        code,
                        match.Clock.ActivePlayerDeadline(),
                        ct);
                }
                else
                {
                    await _rooms.SaveAsync(room, ct);
                }

                return AppResult<SubmitSetupResult>.Ok(new SubmitSetupResult(
                    Room: RoomMapper.ToDto(room, now, _games),
                    MatchStarted: matchStarted));
            }, ct);
        }
        catch (LockTimeoutException)
        {
            return AppResult<SubmitSetupResult>.Fail(PlatformErrors.RoomBusy);
        }
    }
}
