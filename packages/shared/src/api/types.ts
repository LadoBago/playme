// @manually-maintained — kept in sync by hand until `pnpm gen:api`
// regenerates this file from the API's OpenAPI document.
//
// Source of truth: apps/api/src/PlayMe.Application/Dtos (C# records).
// If you change a DTO on the API side, update this file in the same
// PR — there is no autogeneration today. The Zod schemas in
// `./schemas.ts` carry compile-time drift guards that catch most
// structural mismatches.

export type RoomStatus =
  | 'waitingForOpponent'
  | 'inProgress'
  | 'ended'
  | 'awaitingRematch'
  | 'closed'
  | 'expired';

export type Role = 'host' | 'challenger';

export type SideSelectionMode = 'hostPicksSpecific' | 'random' | 'challengerPicks';

export type OutcomeKind = 'win' | 'draw' | 'resign' | 'timeout';

export interface OutcomeDto {
  kind: OutcomeKind;
  winningSide?: string;
  resigningSide?: string;
  timedOutSide?: string;
}

export interface PlayerDto {
  displayName: string;
  side?: string;
}

/**
 * Server-authoritative chess-clock snapshot. Server emits this stamped at
 * `serverNowAt`; the client extrapolates the active player's remaining time
 * locally between snapshots via `Date.now() - serverNowAt`.
 */
export interface ClockSnapshotDto {
  hostMs: number;
  challengerMs: number;
  activePlayer: Role;
  lastTickAt: string;
  serverNowAt: string;
}

export interface MatchDto {
  gameId: string;
  sideToMove: string;
  moveCount: number;
  /**
   * Opaque per-game state blob (JSON string produced by the server-side
   * `IGameModule.Serialize`). The platform never inspects the shape; the
   * per-game web renderer is responsible for `JSON.parse(state)` and
   * understanding its own shape. See CLAUDE.md §7 "Platform thinness".
   */
  state: string;
  clock: ClockSnapshotDto;
  outcome?: OutcomeDto;
}

/**
 * Session-only series scoreboard. Counts roll up across rematches in the
 * same room and reset only when the room itself dies. Win = 1 point;
 * Draw = 0 points but `draws` is tracked for display context.
 * See docs/platform-and-games.md §1 #13.
 */
export interface ScoreDto {
  host: number;
  challenger: number;
  draws: number;
}

export interface RoomDto {
  code: string;
  gameId: string;
  sideSelectionMode: SideSelectionMode;
  status: RoomStatus;
  host: PlayerDto;
  challenger?: PlayerDto;
  hostConnected: boolean;
  challengerConnected: boolean;
  currentMatch?: MatchDto;
  createdAt: string;
  score: ScoreDto;
}

/**
 * Caller-scoped view of a room. Returned by RoomHub.JoinRoom so the web
 * client learns which seat its (HttpOnly, encrypted) session cookie
 * authorizes it for — the client cannot decode the cookie itself.
 */
export interface RoomSessionDto {
  role: Role;
  room: RoomDto;
}

/**
 * Wire-level move payload. `payload` is opaque to the platform — its shape
 * is an agreement between the per-game server module's `IGameMoveParser`
 * and the per-game web renderer. TTT uses `{cell: 0..8}`, Connect 4 will
 * use `{column: 0..6}`, etc. See CLAUDE.md §7 "Platform thinness".
 */
export interface MoveDto {
  payload: unknown;
}

export interface ProblemDetailsResponse {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
}

export interface CreateRoomRequest {
  hostDisplayName: string;
  gameId: string;
  sideSelectionMode: SideSelectionMode;
  hostSide?: string | undefined;
}

export interface JoinRoomRequestBody {
  displayName: string;
  side?: string | undefined;
}
