// TS types mirroring the PlayMe.Api DTOs. The C# records are the source of
// truth (apps/api/src/PlayMe.Application/Dtos); these stay in sync with
// them. A later sprint will wire `pnpm gen:api` to regenerate this file
// from the API's OpenAPI document (tools/gen-api/).

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

export interface BoardCoordinate {
  row: number;
  col: number;
}

export interface OutcomeDto {
  kind: OutcomeKind;
  winningSide?: string;
  resigningSide?: string;
  timedOutSide?: string;
  winningLine?: readonly BoardCoordinate[];
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
  rows: number;
  cols: number;
  cells: readonly (string | null)[];
  clock: ClockSnapshotDto;
  outcome?: OutcomeDto;
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

export interface MoveDto {
  cell?: number;
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
