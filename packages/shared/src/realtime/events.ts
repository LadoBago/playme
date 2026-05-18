// Server → client event names broadcast by the C# RoomHub
// (apps/api/src/PlayMe.Api/Hubs/RoomHubEvents.cs). Keep in sync.

import type { Role, RoomDto } from '../api/types';

export const RoomHubEvent = {
  OpponentJoined: 'OpponentJoined',
  MatchStarted: 'MatchStarted',
  MoveAccepted: 'MoveAccepted',
  MatchEnded: 'MatchEnded',
  ClockTick: 'ClockTick',
  OpponentDisconnected: 'OpponentDisconnected',
  OpponentReconnected: 'OpponentReconnected',
  OpponentExited: 'OpponentExited',
} as const;

export type RoomHubEventName = (typeof RoomHubEvent)[keyof typeof RoomHubEvent];

export interface OpponentJoinedPayload {
  room: RoomDto;
}

export interface MatchStartedPayload {
  room: RoomDto;
}

export interface MoveAcceptedPayload {
  room: RoomDto;
}

export interface MatchEndedPayload {
  room: RoomDto;
}

export interface OpponentDisconnectedPayload {
  role: Role;
}

export interface OpponentReconnectedPayload {
  role: Role;
  room: RoomDto;
}

export interface OpponentExitedPayload {
  role: Role;
  room: RoomDto;
}
