import { z } from 'zod';

// Wire shapes owned by the seabattle module — the agreement with the C#
// SeaBattleGameModule (docs/games/seabattle.md "Wire vocabulary"). Two
// shapes arrive on `MatchDto.state`: the live per-viewer projection
// (`SerializeFor`, platform seam A) and — once the match is terminal —
// the full reveal (`Serialize`). The view normalizes both into one model.

export const GRID_SIZE = 10;
export const FLEET_CELL_COUNT = 20;

export const ShipSchema = z.object({
  x: z.number().int(),
  y: z.number().int(),
  length: z.number().int(),
  horizontal: z.boolean(),
});
export type Ship = z.infer<typeof ShipSchema>;

export const ShotResultSchema = z.enum(['miss', 'hit', 'sunk']);
export type ShotResult = z.infer<typeof ShotResultSchema>;

export const ShotViewSchema = z.object({
  x: z.number().int(),
  y: z.number().int(),
  result: ShotResultSchema,
});
export type ShotView = z.infer<typeof ShotViewSchema>;

/** Live per-viewer projection (`SerializeFor`). */
export const SeaBattleProjectionSchema = z.object({
  phase: z.enum(['setup', 'battle']),
  viewerSide: z.string().optional(),
  yourFleet: z.array(ShipSchema).optional(),
  shots: z.object({
    first: z.array(ShotViewSchema),
    second: z.array(ShotViewSchema),
  }),
  sunk: z.object({
    first: z.array(ShipSchema),
    second: z.array(ShipSchema),
  }),
});
export type SeaBattleProjection = z.infer<typeof SeaBattleProjectionSchema>;

/** Terminal full reveal (`Serialize`) — fleets + raw shot order. */
export const SeaBattleFullStateSchema = z.object({
  firstFleet: z.array(ShipSchema).optional(),
  secondFleet: z.array(ShipSchema).optional(),
  shotsByFirst: z.array(z.object({ x: z.number().int(), y: z.number().int() })).optional(),
  shotsBySecond: z.array(z.object({ x: z.number().int(), y: z.number().int() })).optional(),
});
export type SeaBattleFullState = z.infer<typeof SeaBattleFullStateSchema>;

export function shipCells(ship: Ship): { x: number; y: number }[] {
  return Array.from({ length: ship.length }, (_, i) =>
    ship.horizontal ? { x: ship.x + i, y: ship.y } : { x: ship.x, y: ship.y + i },
  );
}

export function cellKey(x: number, y: number): number {
  return y * GRID_SIZE + x;
}
