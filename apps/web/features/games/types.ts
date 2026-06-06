import type { Locale } from '@playme/shared';
import type { ComponentType, ReactNode } from 'react';

/**
 * Per-game web renderer contract. The platform room shell (`room-client.tsx`)
 * resolves a `GameView` by `gameId` and hands it the state blob, the caller's
 * side, and a callback for submitting moves. Every payload type, every reject
 * key, every rendering choice (grid-of-cells vs. column-drop, X/O glyphs vs.
 * disc/ring) is owned by the module — the platform never inspects any of it
 * (CLAUDE.md §7 "Platform thinness").
 */
export interface GameViewProps {
  /** Opaque per-game serialized state from `MatchDto.state`. Each module
   *  parses its own shape. */
  readonly matchState: string;
  /** The caller's side ("x"/"o" for Tic-Tac-Toe, "red"/"yellow" for
   *  Connect 4) or null while role detection is in flight. */
  readonly callerSide: string | null;
  /** True iff the match is in progress AND it's the caller's turn. The
   *  module uses this to gate clickability. */
  readonly canPlay: boolean;
  /** True when the match has ended. The module uses this to freeze the
   *  board (drop hover affordances, lock pointer events). */
  readonly matchEnded: boolean;
  /** Submit a move. The payload shape is the module's contract with its
   *  matching `IGameMoveParser` on the API side — the platform passes it
   *  through opaquely to `RoomHub.SubmitMove`. */
  readonly onSubmitMove: (payload: unknown) => void;
  /** Setup-phase readiness (Sprint 10 seam C), already resolved to the
   *  caller's perspective. Null/undefined for setup-less games and once a
   *  setup game's room shape no longer carries the flags. A view whose own
   *  parsed state reports the setup phase renders its placement screen off
   *  these. */
  readonly setup?: { readonly mineCommitted: boolean; readonly opponentCommitted: boolean } | null;
  /** Submit the one-and-final setup commit (`RoomHub.SubmitSetup`). The
   *  payload shape is the module ↔ `ISetupGame` agreement; the platform
   *  passes it through opaquely. Undefined for setup-less games. */
  readonly onSubmitSetup?: (payload: unknown) => void;
}

export type GameView = (props: GameViewProps) => ReactNode;

/**
 * Props for `GameModule.TurnStatusExtra` — an optional inline annotation
 * the room shell renders on the same row as its "your turn / opponent's
 * turn" pill while the match runs (never during setup or after the
 * outcome). Same opacity rules as `GameViewProps`: the platform passes
 * the state blob through and never inspects what the module renders.
 */
export interface TurnStatusExtraProps {
  /** Opaque per-game serialized state from `MatchDto.state`. */
  readonly matchState: string;
  /** The caller's side in this module's vocab, or null while role
   *  detection is in flight. */
  readonly callerSide: string | null;
  /** True iff it's the caller's turn (mirrors `GameViewProps.canPlay`). */
  readonly canPlay: boolean;
}

/**
 * Per-game module registered with the platform. The view is the renderer
 * the room shell mounts; `getSideLabel` resolves an opaque side string
 * (this module's vocab — "x"/"o", "red"/"yellow", …) to a localised
 * display label so platform UI (match-header etc.) can show "Red"/"X"
 * without importing per-game vocabulary (CLAUDE.md §7 "Platform
 * thinness"). Returns null if `side` isn't a side this game recognises.
 */
export interface GameModule {
  /** `ComponentType` (not the bare `GameView` signature) so the registry
   *  can register the renderer behind `next/dynamic` — only the active
   *  game's view chunk ships to the client. Module authors still write a
   *  plain `GameView` function. */
  readonly View: ComponentType<GameViewProps>;
  readonly getSideLabel: (side: string, locale: Locale) => string | null;
  /** Optional inline annotation rendered beside the platform's turn pill
   *  (e.g. Sea Battle's hit/miss/sunk shot feedback). Omitted by games
   *  with nothing to report — the turn row is then the pill alone. */
  readonly TurnStatusExtra?: ComponentType<TurnStatusExtraProps>;
}
