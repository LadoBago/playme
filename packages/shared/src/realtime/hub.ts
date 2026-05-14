import * as signalR from '@microsoft/signalr';
import type { ZodType } from 'zod';
import type {
  MatchEndedPayload,
  MatchStartedPayload,
  MoveAcceptedPayload,
  OpponentDisconnectedPayload,
  OpponentJoinedPayload,
  OpponentReconnectedPayload,
} from './events';
import { RoomHubEvent } from './events';
import {
  MatchEndedPayloadSchema,
  MatchStartedPayloadSchema,
  MoveAcceptedPayloadSchema,
  OpponentDisconnectedPayloadSchema,
  OpponentJoinedPayloadSchema,
  OpponentReconnectedPayloadSchema,
} from './schemas';
import { RoomSchema, RoomSessionSchema } from '../api/schemas';
import type { MoveDto, RoomDto, RoomSessionDto } from '../api/types';

/**
 * Typed wrapper around @microsoft/signalr's HubConnection. Keeps the
 * @microsoft/signalr import surface out of features (CLAUDE.md §8 frontend
 * dependency inversion) so swapping transports later is a one-file change.
 *
 * Sprint 2 note: the clock travels piggybacked on the existing
 * room-bearing events (MatchStarted, MoveAccepted, MatchEnded,
 * OpponentReconnected, and the JoinRoom reply). The standalone
 * <c>ClockTick</c> event name is reserved for a future drift-correction
 * sweep but is not subscribed here today.
 */
export interface RoomHubHandlers {
  onOpponentJoined?: (payload: OpponentJoinedPayload) => void;
  onMatchStarted?: (payload: MatchStartedPayload) => void;
  onMoveAccepted?: (payload: MoveAcceptedPayload) => void;
  onMatchEnded?: (payload: MatchEndedPayload) => void;
  onOpponentDisconnected?: (payload: OpponentDisconnectedPayload) => void;
  onOpponentReconnected?: (payload: OpponentReconnectedPayload) => void;
  /** Fires when the SignalR transport drops and is retrying. */
  onReconnecting?: (error: Error | undefined) => void;
  /** Fires when SignalR has re-established the transport. */
  onReconnected?: () => void;
  /** Fires when the connection has terminated for good (no more retries). */
  onConnectionClosed?: (error: Error | undefined) => void;
}

export interface RoomHubOptions {
  /** Hub URL — defaults to '/hubs/room' (same-origin in dev via Next.js rewrites). */
  url?: string;
}

/**
 * Auto-reconnect schedule covering the 30 s reconnect-grace window
 * (state.md §2.2). The default @microsoft/signalr schedule sums to
 * ~42 s, which works but back-loads its retry at the 10 s mark; we
 * front-load so a brief network blip recovers in well under a second.
 */
const RECONNECT_DELAYS_MS = [0, 250, 500, 1000, 2000, 4000, 8000, 16000];

export class RoomHubClient {
  private readonly _connection: signalR.HubConnection;

  constructor(options: RoomHubOptions = {}) {
    this._connection = new signalR.HubConnectionBuilder()
      .withUrl(options.url ?? '/hubs/room', {
        // The signed session cookie is HttpOnly and rides in via credentials.
        // Don't switch to accessTokenFactory — v1 is cookie-only per §5.4.
        withCredentials: true,
      })
      .withAutomaticReconnect(RECONNECT_DELAYS_MS)
      .build();
  }

  on(handlers: RoomHubHandlers): void {
    this._bindEvent(
      RoomHubEvent.OpponentJoined,
      OpponentJoinedPayloadSchema,
      handlers.onOpponentJoined,
    );
    this._bindEvent(
      RoomHubEvent.MatchStarted,
      MatchStartedPayloadSchema,
      handlers.onMatchStarted,
    );
    this._bindEvent(
      RoomHubEvent.MoveAccepted,
      MoveAcceptedPayloadSchema,
      handlers.onMoveAccepted,
    );
    this._bindEvent(
      RoomHubEvent.MatchEnded,
      MatchEndedPayloadSchema,
      handlers.onMatchEnded,
    );
    this._bindEvent(
      RoomHubEvent.OpponentDisconnected,
      OpponentDisconnectedPayloadSchema,
      handlers.onOpponentDisconnected,
    );
    this._bindEvent(
      RoomHubEvent.OpponentReconnected,
      OpponentReconnectedPayloadSchema,
      handlers.onOpponentReconnected,
    );
    if (handlers.onReconnecting) {
      this._connection.onreconnecting((err) => handlers.onReconnecting!(err ?? undefined));
    }
    if (handlers.onReconnected) {
      this._connection.onreconnected(() => handlers.onReconnected!());
    }
    if (handlers.onConnectionClosed) {
      this._connection.onclose((err) => handlers.onConnectionClosed!(err ?? undefined));
    }
  }

  async start(): Promise<void> {
    await this._connection.start();
  }

  async stop(): Promise<void> {
    await this._connection.stop();
  }

  /**
   * Subscribe to a server-pushed hub event after validating the payload
   * with Zod. A schema mismatch logs a structured error and drops the
   * message rather than calling the handler with an ill-shaped payload —
   * the alternative (trusting the cast) is exactly the
   * "every external input must be parsed" rule (CLAUDE.md §6).
   *
   * The cast at <c>handler(parsed.data as T)</c> is safe because (1) the
   * schema has just validated the runtime shape, and (2) the compile-time
   * drift guards in <c>./schemas.ts</c> prove the schema output is
   * structurally compatible with each payload type. The cast exists only
   * to bridge Zod's optional-key inference (<c>{ k?: T | undefined }</c>)
   * with this project's <c>exactOptionalPropertyTypes: true</c>
   * (<c>{ k?: T }</c>) — a TS strictness mismatch, not a real difference.
   */
  private _bindEvent<T>(
    eventName: string,
    schema: ZodType<unknown>,
    handler: ((payload: T) => void) | undefined,
  ): void {
    if (!handler) return;
    this._connection.on(eventName, (raw: unknown) => {
      const parsed = schema.safeParse(raw);
      if (!parsed.success) {
        console.error('[RoomHub] dropping malformed payload', {
          event: eventName,
          issues: parsed.error.issues,
        });
        return;
      }
      handler(parsed.data as T);
    });
  }

  /**
   * Call Hub.JoinRoom — registers presence in the room (CLAUDE.md §2.4).
   * Returns the room state and the caller's role (decoded from the signed
   * session cookie on the server). The server fires MatchStarted to the
   * group if this call flipped WaitingForOpponent → InProgress, or
   * OpponentReconnected to the other player if this call landed on a
   * reconnect path.
   *
   * <paramref name="expectedRoomCode"/> is the URL's room code. The server
   * validates it against the cookie's session and rejects with
   * <c>errors.session.unauthorized</c> if they don't match — covers the
   * "stale cookie for a previously-joined room" case when the same
   * browser opens a different room's link.
   */
  async joinRoom(expectedRoomCode: string): Promise<RoomSessionDto> {
    const raw = await this._connection.invoke<unknown>('JoinRoom', expectedRoomCode);
    // See _bindEvent for why the cast is needed (Zod ↔ exactOptionalPropertyTypes).
    return RoomSessionSchema.parse(raw) as unknown as RoomSessionDto;
  }

  /**
   * Call Hub.SubmitMove. Resolves with the new room state on success;
   * rejects with a SignalR error whose message is the i18n key
   * (errors.move.illegalCell, errors.move.cellOccupied,
   * errors.move.notYourTurn, ...).
   */
  async submitMove(move: MoveDto): Promise<RoomDto> {
    const raw = await this._connection.invoke<unknown>('SubmitMove', move);
    return RoomSchema.parse(raw) as unknown as RoomDto;
  }

  get state(): signalR.HubConnectionState {
    return this._connection.state;
  }
}
