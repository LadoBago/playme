namespace PlayMe.Domain.Platform;

/// <summary>
/// Optional capability for game modules that can play one side themselves
/// (Sprint 12 seam D; see docs/bots.md). A module implements this
/// <em>in addition to</em> <see cref="IGameModule"/>; the platform
/// dispatches by capability (<c>module is IBotPlayer</c>), never by
/// <see cref="GameId"/>. A module that does not implement it simply never
/// appears as a bot-capable game — the catalog flag is derived from this
/// check, not declared anywhere.
///
/// <para>
/// <b>What the platform guarantees to the implementation.</b>
/// <see cref="ChooseMove"/> is called only when the room is
/// <see cref="RoomStatus.InProgress"/>, the match has no
/// <see cref="Outcome"/>, and <c>side</c> is the side-to-move. For modules
/// that also implement <see cref="IHiddenStateGame"/> the state has
/// <em>already been projected</em> for <c>side</c> through
/// <see cref="IHiddenStateGame.SerializeFor"/> and re-read via
/// <see cref="IGameModule.Deserialize"/>, so a bot cannot see what its
/// human opponent cannot see — it is honest by construction, not by
/// discipline. Perfect-information modules receive the full state. The
/// call runs <em>outside</em> the room lock; the platform re-validates the
/// side-to-move and move count before applying the returned move, so a
/// slow bot can lose on time but can never corrupt a match.
/// </para>
///
/// <para>
/// <b>What the implementation guarantees to the platform.</b> The returned
/// move is legal: <see cref="IGameModule.ApplyMove"/> on the same state and
/// side accepts it. An illegal return is a module bug and is surfaced as
/// an error, never retried. The call is pure apart from <c>rng</c>: no
/// I/O, no shared mutable state, no wall-clock reads — every source of
/// randomness goes through <c>rng</c> so a seeded test reproduces the
/// exact move. The call is bounded: the target is 1000 ms on the production
/// tier at <see cref="BotLevel.Hard"/> for the largest board the module
/// supports; modules enforce it with a depth cap, not a timer.
/// </para>
/// </summary>
public interface IBotPlayer
{
    /// <summary>
    /// Pick the move the bot plays as <paramref name="side"/> from
    /// <paramref name="state"/> at <paramref name="level"/>. The move
    /// shape is the module's own <see cref="GameMove"/> subtype — the same
    /// one its <c>IGameMoveParser</c> produces for human moves, so the
    /// apply path is identical for both.
    /// </summary>
    GameMove ChooseMove(IGameState state, string side, BotLevel level, IRandomSource rng);
}
