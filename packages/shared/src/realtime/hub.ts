import * as signalR from '@microsoft/signalr';
import type {
  MatchEndedPayload,
  MatchStartedPayload,
  MoveAcceptedPayload,
  OpponentDisconnectedPayload,
  OpponentJoinedPayload,
} from './events';
import { RoomHubEvent } from './events';
import type { MoveDto, RoomDto } from '../api/types';

/**
 * Typed wrapper around @microsoft/signalr's HubConnection. Keeps the
 * @microsoft/signalr import surface out of features (CLAUDE.md §8 frontend
 * dependency inversion) so swapping transports later is a one-file change.
 */
export interface RoomHubHandlers {
  onOpponentJoined?: (payload: OpponentJoinedPayload) => void;
  onMatchStarted?: (payload: MatchStartedPayload) => void;
  onMoveAccepted?: (payload: MoveAcceptedPayload) => void;
  onMatchEnded?: (payload: MatchEndedPayload) => void;
  onOpponentDisconnected?: (payload: OpponentDisconnectedPayload) => void;
  onConnectionClosed?: (error: Error | undefined) => void;
}

export interface RoomHubOptions {
  /** Hub URL — defaults to '/hubs/room' (same-origin in dev via Next.js rewrites). */
  url?: string;
}

export class RoomHubClient {
  private readonly _connection: signalR.HubConnection;

  constructor(options: RoomHubOptions = {}) {
    this._connection = new signalR.HubConnectionBuilder()
      .withUrl(options.url ?? '/hubs/room', {
        // The signed session cookie is HttpOnly and rides in via credentials.
        // Don't switch to accessTokenFactory — v1 is cookie-only per §5.4.
        withCredentials: true,
      })
      .withAutomaticReconnect()
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
   * Returns the current room state; the server fires MatchStarted to the
   * group if this call flipped WaitingForOpponent → InProgress.
   */
  joinRoom(): Promise<RoomDto> {
    return this._connection.invoke<RoomDto>('JoinRoom');
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
