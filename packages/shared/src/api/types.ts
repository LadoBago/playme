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
  | 'settingUp'
  | 'inProgress'
  | 'ended'
  | 'awaitingRematch'
  | 'closed'
  | 'expired';

export type Role = 'host' | 'challenger';

export type SideSelectionMode = 'hostPicksSpecific' | 'random' | 'challengerPicks';

export type OutcomeKind = 'win' | 'draw' | 'resign' | 'timeout' | 'disconnect';

export interface OutcomeDto {
  kind: OutcomeKind;
  winningSide?: string;
  resigningSide?: string;
  timedOutSide?: string;
  losingSide?: string;
}

/** Who occupies a seat (Sprint 12 seam D). The host is always `human`. */
export type PlayerKind = 'human' | 'bot';

/** Strength of a bot opponent, chosen by the host at room creation. */
export type BotLevel = 'easy' | 'medium' | 'hard';

export interface PlayerDto {
  displayName: string;
  side?: string;
  /**
   * Seat kind (Sprint 12 seam D). For a `bot` the `displayName` is a
   * placeholder — render the bot from this field through an i18n key.
   * Absent on payloads from an API that predates the field; treat as
   * `human`.
   */
  kind?: PlayerKind;
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
  /**
   * Setup-phase commitment flags (Sprint 10 seam C). Present only for
   * games whose module implements `ISetupGame` (e.g. sea battle); absent
   * for setup-less games. Role-level readiness only — setup payloads live
   * inside the opaque per-game `state` and, for hidden-state games, never
   * reach the opponent.
   */
  setup?: SetupStateDto;
}

export interface SetupStateDto {
  hostCommitted: boolean;
  challengerCommitted: boolean;
}

/**
 * Session-only series scoreboard. Counts roll up across rematches in the
 * same room and reset only when the room itself dies. Win = 1 point;
 * Draw = 0 points but `draws` is tracked for display context.
 * See docs/platform.md §1 #13.
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
  /** Role that offered the current rematch — set while status is
   * `awaitingRematch`, null otherwise. See docs/platform.md §1 #10. */
  rematchOffererRole?: Role;
  /**
   * Opaque per-room game-options blob the host chose at room creation
   * (Sprint 9 PR1). Platform never inspects the shape — each game module
   * owns its own schema (e.g. unified `tictactoe` carries `{boardSize}`).
   * Null / undefined for games without options.
   */
  gameOptions?: unknown;
  /**
   * Strength of the bot in the challenger seat (Sprint 12 seam D).
   * Absent for human-vs-human rooms.
   */
  botLevel?: BotLevel;
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
  /**
   * Opaque per-room game-options blob sent at room creation (Sprint 9 PR1).
   * Server-side game module validates the shape; surface size cap is 1 KiB
   * of raw JSON (see CreateRoomCommandValidator on the API).
   */
  gameOptions?: unknown;
  /**
   * Who fills the challenger seat (Sprint 12 seam D). `bot` creates a
   * solo room with no invite link; the server rejects it for a game
   * whose module has no bot (see `GameCatalogEntryDto.supportsBot`).
   * Defaults to `human`.
   */
  opponent?: PlayerKind;
  /** Required when `opponent` is `bot`, forbidden otherwise. */
  botLevel?: BotLevel;
}

/**
 * One row of `GET /api/games`: capabilities only the server can answer
 * (Sprint 12 seam D). Names, rules and artwork stay in the static web
 * catalog.
 */
export interface GameCatalogEntryDto {
  id: string;
  supportsBot: boolean;
}

export interface JoinRoomRequestBody {
  displayName: string;
  side?: string | undefined;
}
