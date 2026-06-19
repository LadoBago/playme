import * as signalR from '@microsoft/signalr';
import type { ZodType } from 'zod';
import type {
  EmoteReceivedPayload,
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
import { RoomHubEvent } from './events';
import type { EmoteId } from './emotes';
import {
  EmoteReceivedPayloadSchema,
  MatchEndedPayloadSchema,
  MatchStartedPayloadSchema,
  MoveAcceptedPayloadSchema,
  OpponentDisconnectedPayloadSchema,
  OpponentExitedPayloadSchema,
  OpponentJoinedPayloadSchema,
  OpponentReconnectedPayloadSchema,
  OpponentSetupCommittedPayloadSchema,
  RematchDeclinedPayloadSchema,
  RematchOfferedPayloadSchema,
  RoomExpiredPayloadSchema,
  SetupStartedPayloadSchema,
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
  /** Setup games only (Sprint 10 seam C): the room entered `settingUp`. */
  onSetupStarted?: (payload: SetupStartedPayload) => void;
  /** Setup games only: the opponent committed; readiness signal, no payload content. */
  onOpponentSetupCommitted?: (payload: OpponentSetupCommittedPayload) => void;
  onMoveAccepted?: (payload: MoveAcceptedPayload) => void;
  onMatchEnded?: (payload: MatchEndedPayload) => void;
  onOpponentDisconnected?: (payload: OpponentDisconnectedPayload) => void;
  onOpponentReconnected?: (payload: OpponentReconnectedPayload) => void;
  onOpponentExited?: (payload: OpponentExitedPayload) => void;
  onRematchOffered?: (payload: RematchOfferedPayload) => void;
  onRematchDeclined?: (payload: RematchDeclinedPayload) => void;
  /** The opponent sent an in-match emote — render it as a transient bubble. */
  onEmoteReceived?: (payload: EmoteReceivedPayload) => void;
  /**
   * Fires when the server reaps a `WaitingForOpponent` room whose
   * 30-minute deadline elapsed without a challenger joining. The
   * room's Redis state is gone by the time this lands; the client
   * should render an "expired" state rather than try to refetch.
   */
  onRoomExpired?: (payload: RoomExpiredPayload) => void;
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
 * Front-loaded retry schedule that then settles to a steady 4 s cadence.
 * The first six attempts recover a brief blip in well under a second; past
 * that the policy keeps retrying every 4 s.
 */
const RECONNECT_SCHEDULE_MS = [0, 250, 500, 1000, 2000, 4000];
const RECONNECT_STEADY_MS = 4_000;

/**
 * Reconnect policy that **never gives up** while the page is open. The
 * default finite @microsoft/signalr schedule stops after its last entry
 * and fires `onclose`, stranding a still-reconnectable room until a manual
 * browser refresh — the bug this replaces. Returning a delay for every
 * retry count (never `null`) keeps SignalR trying, so a network outage of
 * any length recovers on its own once connectivity returns; the flat 4 s
 * ceiling bounds the worst-case wait.
 *
 * It deliberately does NOT stop at the server's disconnect-grace deadline:
 * the client can't observe that deadline, grace may never start (it only
 * ticks on the disconnected player's turn), and reconnecting *after* a
 * loss is precisely how the player sees the outcome and the rematch offer.
 * Reconnect therefore runs until the room is genuinely terminal — at which
 * point the caller tears the hub down (RoomExpired) or the rejoin returns
 * the closed snapshot and settles.
 */
class IndefiniteReconnectPolicy implements signalR.IRetryPolicy {
  nextRetryDelayInMilliseconds(retryContext: signalR.RetryContext): number {
    return RECONNECT_SCHEDULE_MS[retryContext.previousRetryCount] ?? RECONNECT_STEADY_MS;
  }
}

export class RoomHubClient {
  private readonly _connection: signalR.HubConnection;

  constructor(options: RoomHubOptions = {}) {
    this._connection = new signalR.HubConnectionBuilder()
      .withUrl(options.url ?? '/hubs/room', {
        // The signed session cookie is HttpOnly and rides in via credentials.
        // Don't switch to accessTokenFactory — v1 is cookie-only per §5.4.
        withCredentials: true,
        // Connect straight over WebSockets and skip the negotiate POST.
        // We require WebSockets anyway (no long-polling fallback by design),
        // and the API is stateless behind a Redis backplane, so there's no
        // sticky-session need that negotiate would satisfy. Skipping it
        // removes a round-trip on every connect *and* a failure mode seen in
        // the field: a `negotiate` request that hangs/times-out (notably
        // through a reverse proxy) would stall reconnection; a direct WS
        // upgrade has no such intermediate step. Auth still rides on the WS
        // handshake — the session cookie is SameSite=Lax and the web + API
        // share an eTLD+1, so the browser attaches it on the upgrade
        // (see SessionCookieOptions). `skipNegotiation` *requires* a single
        // explicit transport.
        skipNegotiation: true,
        transport: signalR.HttpTransportType.WebSockets,
      })
      // Quiet SignalR's own logger to Critical only. During an outage the
      // indefinite auto-reconnect retries forever, and SignalR logs every
      // failed negotiate/transport attempt at Error level — a console (and
      // Next dev-overlay) flood for failures that are expected and already
      // handled: the room UI reflects the live/reconnecting/lost state, and
      // the room-client stall watchdog owns actual recovery. Critical keeps
      // genuinely catastrophic logs while dropping the reconnect spam.
      .configureLogging(signalR.LogLevel.Critical)
      .withAutomaticReconnect(new IndefiniteReconnectPolicy())
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
      RoomHubEvent.SetupStarted,
      SetupStartedPayloadSchema,
      handlers.onSetupStarted,
    );
    this._bindEvent(
      RoomHubEvent.OpponentSetupCommitted,
      OpponentSetupCommittedPayloadSchema,
      handlers.onOpponentSetupCommitted,
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
    this._bindEvent(
      RoomHubEvent.OpponentExited,
      OpponentExitedPayloadSchema,
      handlers.onOpponentExited,
    );
    this._bindEvent(
      RoomHubEvent.RematchOffered,
      RematchOfferedPayloadSchema,
      handlers.onRematchOffered,
    );
    this._bindEvent(
      RoomHubEvent.RematchDeclined,
      RematchDeclinedPayloadSchema,
      handlers.onRematchDeclined,
    );
    this._bindEvent(
      RoomHubEvent.RoomExpired,
      RoomExpiredPayloadSchema,
      handlers.onRoomExpired,
    );
    this._bindEvent(
      RoomHubEvent.EmoteReceived,
      EmoteReceivedPayloadSchema,
      handlers.onEmoteReceived,
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
   * Invoke a hub method, unwrapping SignalR's HubException prefix so callers
   * see the bare i18n key. With <c>EnableDetailedErrors=false</c> (the default
   * for production), ASP.NET Core wraps every <c>HubException("errors.X")</c>
   * as <c>"An unexpected error occurred invoking 'METHOD' on the server.
   * HubException: errors.X"</c> before sending it on the wire — left
   * un-stripped, the room page would try to translate that whole framework
   * string as an i18n key and surface the literal text to the player.
   */
  private async _invoke<T>(method: string, ...args: unknown[]): Promise<T> {
    try {
      return await this._connection.invoke<T>(method, ...args);
    } catch (e) {
      if (e instanceof Error) {
        const marker = 'HubException: ';
        const idx = e.message.indexOf(marker);
        if (idx !== -1) {
          throw new Error(e.message.slice(idx + marker.length).trim());
        }
      }
      throw e;
    }
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
    const raw = await this._invoke<unknown>('JoinRoom', expectedRoomCode);
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
    const raw = await this._invoke<unknown>('SubmitMove', move);
    return RoomSchema.parse(raw) as unknown as RoomDto;
  }

  /**
   * Call Hub.SubmitSetup — the one-and-final setup commit for setup
   * games (Sprint 10 seam C; docs/games/seabattle.md). The payload shape
   * is the module ↔ renderer agreement, opaque to the platform. Resolves
   * with the new room state; rejects with i18n keys
   * (errors.setup.notInSetup, errors.setup.alreadyCommitted, or the
   * module's own validation keys).
   */
  async submitSetup(setup: MoveDto): Promise<RoomDto> {
    const raw = await this._invoke<unknown>('SubmitSetup', setup);
    return RoomSchema.parse(raw) as unknown as RoomDto;
  }

  /**
   * Call Hub.Resign — voluntary in-progress concession
   * (docs/platform.md §1 #8). Caller is expected to have
   * collected an explicit confirmation before invoking. Resolves with
   * the post-resign room state; rejects with i18n keys
   * (errors.move.matchNotInProgress, errors.rate.exceeded, ...).
   */
  async resign(): Promise<RoomDto> {
    const raw = await this._invoke<unknown>('Resign');
    return RoomSchema.parse(raw) as unknown as RoomDto;
  }

  /**
   * Call Hub.ExitRoom — voluntary post-match exit (docs/state.md §2.4).
   * Valid in Ended / AwaitingRematch; idempotent on Closed (resolves
   * silently). Resolves with the post-exit room state; rejects with
   * errors.exit.notAllowed for invalid states.
   */
  async exitRoom(): Promise<RoomDto> {
    const raw = await this._invoke<unknown>('ExitRoom');
    return RoomSchema.parse(raw) as unknown as RoomDto;
  }

  /**
   * Call Hub.OfferRematch — first step of the rematch handshake
   * (docs/platform.md §1 #10). Returns the room post-call;
   * resolves with status `awaitingRematch` on the first offer, or with
   * status `inProgress` if a simultaneous offer from the opposite role
   * raced this one (implicit accept).
   */
  async offerRematch(): Promise<RoomDto> {
    const raw = await this._invoke<unknown>('OfferRematch');
    return RoomSchema.parse(raw) as unknown as RoomDto;
  }

  /**
   * Call Hub.AcceptRematch — responder side. Swaps sides and starts a
   * fresh match.
   */
  async acceptRematch(): Promise<RoomDto> {
    const raw = await this._invoke<unknown>('AcceptRematch');
    return RoomSchema.parse(raw) as unknown as RoomDto;
  }

  /**
   * Call Hub.RejectRematch — responder side. Closes the room; the
   * caller's UI should route back to the lobby (asymmetric exit per
   * §1 #10).
   */
  async rejectRematch(): Promise<RoomDto> {
    const raw = await this._invoke<unknown>('RejectRematch');
    return RoomSchema.parse(raw) as unknown as RoomDto;
  }

  /**
   * Call Hub.SendEmote — relay an in-match emote to the opponent. Fire-and-
   * forget: the server returns nothing, validates the id against the shared
   * allowlist, and silently drops rate-limited or out-of-phase sends. Resolves
   * once the server has accepted the call; rejects only on a genuine error
   * (e.g. errors.emote.unknown for an id outside the allowlist, or
   * errors.session.unauthorized).
   */
  async sendEmote(emoteId: EmoteId): Promise<void> {
    await this._invoke<void>('SendEmote', emoteId);
  }

  get state(): signalR.HubConnectionState {
    return this._connection.state;
  }
}
