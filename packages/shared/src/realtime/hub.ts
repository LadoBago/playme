import * as signalR from '@microsoft/signalr';
import type {
  MatchEndedPayload,
  MatchStartedPayload,
  MoveAcceptedPayload,
  OpponentDisconnectedPayload,
  OpponentJoinedPayload,
  OpponentReconnectedPayload,
} from './events';
import { RoomHubEvent } from './events';
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
    if (handlers.onOpponentJoined) {
      this._connection.on(RoomHubEvent.OpponentJoined, handlers.onOpponentJoined);
    }
    if (handlers.onMatchStarted) {
      this._connection.on(RoomHubEvent.MatchStarted, handlers.onMatchStarted);
    }
    if (handlers.onMoveAccepted) {
      this._connection.on(RoomHubEvent.MoveAccepted, handlers.onMoveAccepted);
    }
    if (handlers.onMatchEnded) {
      this._connection.on(RoomHubEvent.MatchEnded, handlers.onMatchEnded);
    }
    if (handlers.onOpponentDisconnected) {
      this._connection.on(
        RoomHubEvent.OpponentDisconnected,
        handlers.onOpponentDisconnected,
      );
    }
    if (handlers.onOpponentReconnected) {
      this._connection.on(
        RoomHubEvent.OpponentReconnected,
        handlers.onOpponentReconnected,
      );
    }
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
  joinRoom(expectedRoomCode: string): Promise<RoomSessionDto> {
    return this._connection.invoke<RoomSessionDto>('JoinRoom', expectedRoomCode);
  }

  /**
   * Call Hub.SubmitMove. Resolves with the new room state on success;
   * rejects with a SignalR error whose message is the i18n key
   * (errors.move.illegalCell, errors.move.cellOccupied,
   * errors.move.notYourTurn, ...).
   */
  submitMove(move: MoveDto): Promise<RoomDto> {
    return this._connection.invoke<RoomDto>('SubmitMove', move);
  }

  get state(): signalR.HubConnectionState {
    return this._connection.state;
  }
}
