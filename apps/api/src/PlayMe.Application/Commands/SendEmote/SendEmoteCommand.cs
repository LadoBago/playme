using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.SendEmote;

/// <summary>
/// Hub <c>SendEmote</c> dispatch. An emote is an ephemeral player-to-player
/// signal (CLAUDE.md §7 "Platform thinness") — not game state and not room
/// state: nothing is persisted, the room is never mutated, and the handler
/// neither takes the room lock nor replays on reconnect. The caller identity
/// (<see cref="CallerPlayerId"/>, <see cref="CallerRole"/>) comes from the
/// signed session, never the client.
/// </summary>
public sealed record SendEmoteCommand(
    string RoomCode,
    string CallerPlayerId,
    Role CallerRole,
    string EmoteId);
