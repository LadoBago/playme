using System.Text.Json;

namespace PlayMe.Application.Dtos;

/// <summary>
/// Wire-level move payload. The platform routes <see cref="Payload"/>
/// opaquely from the web caller to the registered <see cref="Abstractions.IGameMoveParser"/>
/// for the room's game; the platform never inspects the shape (CLAUDE.md §7
/// "Platform thinness"). Tic-Tac-Toe parsers read <c>{"cell": 0..8}</c>;
/// Connect 4 will read <c>{"column": 0..6}</c>; chess will read
/// <c>{"from": ..., "to": ..., "promote": ...}</c>. None of those keys are
/// known to the platform.
/// </summary>
public sealed record MoveDto(JsonElement Payload);
