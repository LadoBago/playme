using PlayMe.Application.Dtos;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.JoinRoom;

public sealed record JoinRoomResult(PlayerId ChallengerPlayerId, RoomDto Room);
