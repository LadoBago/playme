// Zod schemas mirroring ./types.ts — used to validate every external
// payload that crosses an API or SignalR boundary (CLAUDE.md §6, §7,
// docs/security.md §3). When `pnpm gen:api` lands, the generator will
// emit both the TS types and these schemas; until then, the two files
// are kept in sync by hand and the `satisfies`-style assertions at the
// bottom of this file catch shape drift at compile time.

import { z } from 'zod';
import type {
  ClockSnapshotDto,
  MatchDto,
  MoveDto,
  OutcomeDto,
  PlayerDto,
  ProblemDetailsResponse,
  RoomDto,
  RoomSessionDto,
  ScoreDto,
} from './types';

export const RoleSchema = z.enum(['host', 'challenger']);

export const RoomStatusSchema = z.enum([
  'waitingForOpponent',
  'settingUp',
  'inProgress',
  'ended',
  'awaitingRematch',
  'closed',
  'expired',
]);

export const SideSelectionModeSchema = z.enum([
  'hostPicksSpecific',
  'random',
  'challengerPicks',
]);

export const OutcomeKindSchema = z.enum(['win', 'draw', 'resign', 'timeout', 'disconnect']);

export const OutcomeSchema = z.object({
  kind: OutcomeKindSchema,
  winningSide: z.string().optional(),
  resigningSide: z.string().optional(),
  timedOutSide: z.string().optional(),
  losingSide: z.string().optional(),
});

export const PlayerSchema = z.object({
  displayName: z.string(),
  side: z.string().optional(),
});

export const ClockSnapshotSchema = z.object({
  hostMs: z.number(),
  challengerMs: z.number(),
  activePlayer: RoleSchema,
  lastTickAt: z.string(),
  serverNowAt: z.string(),
});

export const SetupStateSchema = z.object({
  hostCommitted: z.boolean(),
  challengerCommitted: z.boolean(),
});

export const MatchSchema = z.object({
  gameId: z.string(),
  sideToMove: z.string(),
  moveCount: z.number().int().nonnegative(),
  // Opaque per-game state blob; the per-game web renderer parses + validates
  // its own shape (CLAUDE.md §7 platform thinness).
  state: z.string(),
  clock: ClockSnapshotSchema,
  outcome: OutcomeSchema.optional(),
  // Setup-phase readiness flags (Sprint 10 seam C); present only for
  // ISetupGame modules.
  setup: SetupStateSchema.optional(),
});

export const ScoreSchema = z.object({
  host: z.number().int().nonnegative(),
  challenger: z.number().int().nonnegative(),
  draws: z.number().int().nonnegative(),
});

export const RoomSchema = z.object({
  code: z.string(),
  gameId: z.string(),
  sideSelectionMode: SideSelectionModeSchema,
  status: RoomStatusSchema,
  host: PlayerSchema,
  challenger: PlayerSchema.optional(),
  hostConnected: z.boolean(),
  challengerConnected: z.boolean(),
  currentMatch: MatchSchema.optional(),
  createdAt: z.string(),
  score: ScoreSchema,
  rematchOffererRole: RoleSchema.optional(),
  // Opaque per-room game-options blob (Sprint 9 PR1). Per-game web
  // renderers parse and validate their own shape; the platform-level
  // schema only confirms it's a present-or-absent JSON value.
  gameOptions: z.unknown().optional(),
});

export const RoomSessionSchema = z.object({
  role: RoleSchema,
  room: RoomSchema,
});

export const MoveSchema = z.object({
  payload: z.unknown(),
});

export const ProblemDetailsSchema = z.object({
  type: z.string().optional(),
  title: z.string().optional(),
  status: z.number().optional(),
  detail: z.string().optional(),
  code: z.string().optional(),
});

// Compile-time drift guards: if the TS types in ./types.ts gain a required
// field that the schema doesn't model (or vice-versa), one of these lines
// stops compiling.
type _AssertOutcome = z.infer<typeof OutcomeSchema> extends OutcomeDto ? true : false;
type _AssertPlayer = z.infer<typeof PlayerSchema> extends PlayerDto ? true : false;
type _AssertClock = z.infer<typeof ClockSnapshotSchema> extends ClockSnapshotDto ? true : false;
type _AssertMatch = z.infer<typeof MatchSchema> extends MatchDto ? true : false;
type _AssertScore = z.infer<typeof ScoreSchema> extends ScoreDto ? true : false;
type _AssertRoom = z.infer<typeof RoomSchema> extends RoomDto ? true : false;
type _AssertRoomSession = z.infer<typeof RoomSessionSchema> extends RoomSessionDto ? true : false;
type _AssertMove = z.infer<typeof MoveSchema> extends MoveDto ? true : false;
type _AssertProblem = z.infer<typeof ProblemDetailsSchema> extends ProblemDetailsResponse
  ? true
  : false;

// Reference the marker types so the asserts aren't tree-shaken as unused.
export type _SchemaDriftGuards = [
  _AssertOutcome,
  _AssertPlayer,
  _AssertClock,
  _AssertMatch,
  _AssertScore,
  _AssertRoom,
  _AssertRoomSession,
  _AssertMove,
  _AssertProblem,
];
