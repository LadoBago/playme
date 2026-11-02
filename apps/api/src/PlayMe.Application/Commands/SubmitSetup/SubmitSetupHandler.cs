using PlayMe.Application.Abstractions;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Application.RateLimiting;
using PlayMe.Application.Setup;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.SubmitSetup;

/// <summary>
/// The setup-commit pipeline for a human caller (Sprint 10 seam C;
/// docs/games/seabattle.md). Inside the room's distributed lock: authorize
/// the caller, verify the room is in <see cref="RoomStatus.SettingUp"/>,
/// reject a double commit (one commit per side, final — a platform rule),
/// then parse and validate the payload via the game module. Applying it
/// and starting play when setup completes is the <see cref="SetupCommitter"/>,
/// shared with the bot path (Sprint 12 seam D).
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
    private readonly IRateLimiter _rateLimiter;
    private readonly SetupCommitter _committer;

    public SubmitSetupHandler(
        IRoomRepository rooms,
        IGameModuleRegistry games,
        IClock clock,
        IRateLimiter rateLimiter,
        SetupCommitter committer)
    {
        _rooms = rooms;
        _games = games;
        _clock = clock;
        _rateLimiter = rateLimiter;
        _committer = committer;
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
                if (stored is null || stored.Id.Value != cmd.CallerPlayerId || stored.IsBot)
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

                var now = _clock.UtcNow;
                var matchStarted = await _committer.CommitAsync(
                    room, setupGame, callerSide, cmd.CallerRole, parseResult.Value!, now, ct);

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
