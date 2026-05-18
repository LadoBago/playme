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
} from './events';

export const OpponentJoinedPayloadSchema = z.object({ room: RoomSchema });
export const MatchStartedPayloadSchema = z.object({ room: RoomSchema });
export const MoveAcceptedPayloadSchema = z.object({ room: RoomSchema });
export const MatchEndedPayloadSchema = z.object({ room: RoomSchema });

export const OpponentDisconnectedPayloadSchema = z.object({ role: RoleSchema });

export const OpponentReconnectedPayloadSchema = z.object({
  role: RoleSchema,
  room: RoomSchema,
});

export const OpponentExitedPayloadSchema = z.object({
  role: RoleSchema,
  room: RoomSchema,
});

type _AssertOpponentJoined = z.infer<typeof OpponentJoinedPayloadSchema> extends OpponentJoinedPayload
  ? true
  : false;
type _AssertMatchStarted = z.infer<typeof MatchStartedPayloadSchema> extends MatchStartedPayload
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

export type _RealtimeSchemaDriftGuards = [
  _AssertOpponentJoined,
  _AssertMatchStarted,
  _AssertMoveAccepted,
  _AssertMatchEnded,
  _AssertOpponentDisconnected,
  _AssertOpponentReconnected,
  _AssertOpponentExited,
];
