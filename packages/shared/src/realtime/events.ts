// Server → client event names broadcast by the C# RoomHub
// (apps/api/src/PlayMe.Api/Hubs/RoomHubEvents.cs). Keep in sync.

import type { RoomDto } from '../api/types';

export const RoomHubEvent = {
  OpponentJoined: 'OpponentJoined',
  MatchStarted: 'MatchStarted',
  MoveAccepted: 'MoveAccepted',
  MatchEnded: 'MatchEnded',
  OpponentDisconnected: 'OpponentDisconnected',
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
  cell: number;
  side: string;
}

export interface MatchEndedPayload {
  room: RoomDto;
}

export interface OpponentDisconnectedPayload {
  role: string;
}
