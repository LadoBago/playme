// Server → client event names broadcast by the C# RoomHub
// (apps/api/src/PlayMe.Api/Hubs/RoomHubEvents.cs). Keep in sync.

import type { Role, RoomDto } from '../api/types';
import type { EmoteId } from './emotes';

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
  EmoteReceived: 'EmoteReceived',
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
 * Why the server reaped the room (docs/state.md §2.3):
 * - `unjoined` — the `WaitingForOpponent` room reached its 30-minute
 *   deadline without anyone joining (`playme:expires` sorted set).
 * - `setupTimeout` — a setup game's deadline elapsed before both
 *   players committed. Setup expiry never awards a win — there is no
 *   forfeit path, regardless of who committed.
 */
export type RoomExpiryReason = 'unjoined' | 'setupTimeout';

/**
 * Fired by the server when it reaps a room outside the normal match
 * lifecycle. The reason tells the client which deadline actually
 * fired so it can render the matching "this room has expired" copy
 * instead of waiting on a subsequent failure.
 */
export interface RoomExpiredPayload {
  reason: RoomExpiryReason;
}

/**
 * An in-match emote the opponent sent (a platform capability, not game
 * state). Carries only the sender's role and the validated emote id — no
 * room state — because an emote mutates nothing. The receiver shows it as a
 * transient bubble and discards it; nothing is persisted or replayed.
 */
export interface EmoteReceivedPayload {
  from: Role;
  emoteId: EmoteId;
}
