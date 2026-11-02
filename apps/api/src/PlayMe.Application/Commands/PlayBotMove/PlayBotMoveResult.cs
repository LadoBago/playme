using PlayMe.Application.Dtos;

namespace PlayMe.Application.Commands.PlayBotMove;

/// <summary>
/// Outcome of a bot-turn dispatch. <see cref="Applied"/> is false when the
/// entry was stale — the room moved on while the bot was thinking (human
/// resigned, match ended, not the bot's turn after all) — and nothing was
/// changed or needs broadcasting. When true, the sweeper broadcasts
/// <c>MoveAccepted</c> unless <see cref="TimedOut"/> (the bot's own clock
/// had already run out: only <c>MatchEnded</c> goes out), plus
/// <c>MatchEnded</c> when <see cref="MatchEnded"/>. <see cref="Forfeited"/>
/// marks the module-fault path (docs/bots.md §2.2): the bot resigned
/// because its module returned an illegal move.
/// </summary>
public sealed record PlayBotMoveResult(
    RoomDto Room,
    bool Applied,
    bool MatchEnded,
    bool TimedOut,
    bool Forfeited);
