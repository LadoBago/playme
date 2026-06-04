// Server → client event names broadcast by the C# RoomHub
// (apps/api/src/PlayMe.Api/Hubs/RoomHubEvents.cs). Keep in sync.

import type { Role, RoomDto } from '../api/types';

export const RoomHubEvent = {
  OpponentJoined: 'OpponentJoined',
  MatchStarted: 'MatchStarted',
  SetupStarted: 'SetupStarted',
  OpponentSetupCommitted: 'OpponentSetupCommitted',
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

/**
 * Fired instead of MatchStarted when a setup game's room enters
 * `settingUp` (Sprint 10 seam C) — both players are present and the
 * placement screen should mount. For hidden-state games the room payload
 * is the receiving player's projection.
 */
export interface SetupStartedPayload {
  room: RoomDto;
}

/**
 * The opponent committed their setup (Sprint 10 seam C). Role-level
 * readiness only — the payload never contains the opponent's setup
 * content. The commit that completes the setup phase sends MatchStarted
 * instead.
 */
export interface OpponentSetupCommittedPayload {
  role: Role;
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
  // Mirrors OpponentReconnected: the still-connected player needs the
  // updated room DTO (the disconnected role's `*Connected` flag is now
  // false) so the UI can render the transient "opponent disconnected"
  // hint. Without it the client never learns about the flag flip until
  // the next state-bearing event arrives.
  room: RoomDto;
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
