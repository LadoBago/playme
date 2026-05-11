namespace PlayMe.Domain.Platform;

/// <summary>
/// Base type for a player's gameplay action. Each game module defines its
/// own concrete subtype (e.g. <c>TicTacToeMove</c> with a cell index,
/// <c>Connect4Move</c> with a column). The platform never inspects payloads;
/// it only passes them through to <see cref="IGameModule.ApplyMove"/>.
/// </summary>
public abstract record GameMove;
