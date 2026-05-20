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
  RematchOffered: 'RematchOffered',
  RematchDeclined: 'RematchDeclined',
  RoomExpired: 'RoomExpired',
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

export interface RematchOfferedPayload {
  offerer: Role;
  room: RoomDto;
}

export interface RematchDeclinedPayload {
  room: RoomDto;
}

/**
 * Fired by the server when a `WaitingForOpponent` room reaches its
 * 30-minute deadline without anyone joining (see docs/state.md §2.2
 * and the `playme:expires` sorted set). Empty payload: the event
 * itself is the signal that the room is gone. The web client renders
 * a clean "this room has expired" state instead of waiting on a
 * subsequent failure.
 */
export type RoomExpiredPayload = Record<string, never>;
