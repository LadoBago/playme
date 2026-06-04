// Zod schemas for every server → client hub payload. Used by RoomHubClient
// to validate inbound SignalR messages before invoking the caller's
// handler (CLAUDE.md §6, docs/security.md §3 "every server-pushed SignalR
// message is parsed through Zod").

import { z } from 'zod';
import { RoleSchema, RoomSchema } from '../api/schemas';
import type {
  MatchEndedPayload,
  MatchStartedPayload,
  MoveAcceptedPayload,
  OpponentDisconnectedPayload,
  OpponentExitedPayload,
  OpponentJoinedPayload,
  OpponentReconnectedPayload,
  OpponentSetupCommittedPayload,
  RematchDeclinedPayload,
  RematchOfferedPayload,
  RoomExpiredPayload,
  SetupStartedPayload,
} from './events';

export const OpponentJoinedPayloadSchema = z.object({ room: RoomSchema });
export const MatchStartedPayloadSchema = z.object({ room: RoomSchema });
export const SetupStartedPayloadSchema = z.object({ room: RoomSchema });
export const MoveAcceptedPayloadSchema = z.object({ room: RoomSchema });
export const MatchEndedPayloadSchema = z.object({ room: RoomSchema });

export const OpponentSetupCommittedPayloadSchema = z.object({
  role: RoleSchema,
  room: RoomSchema,
});

export const OpponentDisconnectedPayloadSchema = z.object({
  role: RoleSchema,
  room: RoomSchema,
});

export const OpponentReconnectedPayloadSchema = z.object({
  role: RoleSchema,
  room: RoomSchema,
});

export const OpponentExitedPayloadSchema = z.object({
  role: RoleSchema,
  room: RoomSchema,
});

export const RematchOfferedPayloadSchema = z.object({
  offerer: RoleSchema,
  room: RoomSchema,
});

export const RematchDeclinedPayloadSchema = z.object({
  room: RoomSchema,
});

// Empty payload — RoomExpired carries no data; the event name is the
// signal. Future-proof against the server adding optional fields by
// not pinning the schema to strict shape.
export const RoomExpiredPayloadSchema = z.object({});

type _AssertOpponentJoined = z.infer<typeof OpponentJoinedPayloadSchema> extends OpponentJoinedPayload
  ? true
  : false;
type _AssertMatchStarted = z.infer<typeof MatchStartedPayloadSchema> extends MatchStartedPayload
  ? true
  : false;
type _AssertSetupStarted = z.infer<typeof SetupStartedPayloadSchema> extends SetupStartedPayload
  ? true
  : false;
type _AssertOpponentSetupCommitted = z.infer<
  typeof OpponentSetupCommittedPayloadSchema
> extends OpponentSetupCommittedPayload
  ? true
  : false;
type _AssertMoveAccepted = z.infer<typeof MoveAcceptedPayloadSchema> extends MoveAcceptedPayload
  ? true
  : false;
type _AssertMatchEnded = z.infer<typeof MatchEndedPayloadSchema> extends MatchEndedPayload
  ? true
  : false;
type _AssertOpponentDisconnected = z.infer<
  typeof OpponentDisconnectedPayloadSchema
> extends OpponentDisconnectedPayload
  ? true
  : false;
type _AssertOpponentReconnected = z.infer<
  typeof OpponentReconnectedPayloadSchema
> extends OpponentReconnectedPayload
  ? true
  : false;
type _AssertOpponentExited = z.infer<
  typeof OpponentExitedPayloadSchema
> extends OpponentExitedPayload
  ? true
  : false;
type _AssertRematchOffered = z.infer<
  typeof RematchOfferedPayloadSchema
> extends RematchOfferedPayload
  ? true
  : false;
type _AssertRematchDeclined = z.infer<
  typeof RematchDeclinedPayloadSchema
> extends RematchDeclinedPayload
  ? true
  : false;
type _AssertRoomExpired = z.infer<typeof RoomExpiredPayloadSchema> extends RoomExpiredPayload
  ? true
  : false;

export type _RealtimeSchemaDriftGuards = [
  _AssertOpponentJoined,
  _AssertMatchStarted,
  _AssertSetupStarted,
  _AssertOpponentSetupCommitted,
  _AssertMoveAccepted,
  _AssertMatchEnded,
  _AssertOpponentDisconnected,
  _AssertOpponentReconnected,
  _AssertOpponentExited,
  _AssertRematchOffered,
  _AssertRematchDeclined,
  _AssertRoomExpired,
];
