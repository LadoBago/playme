using PlayMe.Application.Dtos;
using PlayMe.Application.Errors;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Abstractions;

/// <summary>
/// Parses a wire-level <see cref="MoveDto"/> into the per-game
/// <see cref="GameMove"/> the rules module expects. One implementation per
/// game module, registered in DI; the application's <c>SubmitMoveHandler</c>
/// resolves the right parser via <see cref="IGameModuleRegistry"/>.
///
/// Parsing failures are reported as a typed <see cref="AppResult{T}"/> so
/// the handler can surface a clean error code rather than an exception.
/// </summary>
public interface IGameMoveParser
{
    GameId GameId { get; }

    AppResult<GameMove> Parse(MoveDto dto);
}
