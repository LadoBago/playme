import { GRID_SIZE, shipCells, type Ship } from './schema';

// Client-local random fleet generation (docs/games/seabattle.md "Setup
// phase"): reroll never touches the server — only the final commit does,
// and the C# module re-validates it exhaustively. Placement must be
// unpredictable to the opponent, so randomness comes from
// crypto.getRandomValues (adversarial fairness — the house cryptographic-
// RNG rule, not Math.random()).

/** Ship lengths of the post-Soviet fleet, placed longest-first so the
 *  four-decker always finds room before the grid fills up. */
const FLEET_LENGTHS = [4, 3, 3, 2, 2, 2, 1, 1, 1, 1] as const;

/** Attempts per ship before restarting the whole fleet. Empirically a
 *  full restart is rare (< 1 in ~10⁴ fleets); the cap just bounds the
 *  worst case. */
const ATTEMPTS_PER_SHIP = 200;

/** Unbiased integer in [0, maxExclusive) via rejection sampling. */
function cryptoInt(maxExclusive: number): number {
  const buf = new Uint32Array(1);
  const limit = Math.floor(0x1_0000_0000 / maxExclusive) * maxExclusive;
  for (;;) {
    crypto.getRandomValues(buf);
    const value = buf[0]!;
    if (value < limit) return value % maxExclusive;
  }
}

/** True when `ship` fits the grid and neither overlaps nor touches (even
 *  diagonally) any cell already marked in `blocked`. */
function fits(ship: Ship, blocked: ReadonlySet<number>): boolean {
  for (const cell of shipCells(ship)) {
    if (cell.x < 0 || cell.x >= GRID_SIZE || cell.y < 0 || cell.y >= GRID_SIZE) return false;
    if (blocked.has(cell.y * GRID_SIZE + cell.x)) return false;
  }
  return true;
}

/** Mark a placed ship's cells plus their full 8-neighborhood as blocked —
 *  encodes the no-touch rule for every later placement in one set. */
function block(ship: Ship, blocked: Set<number>): void {
  for (const cell of shipCells(ship)) {
    for (let dy = -1; dy <= 1; dy++) {
      for (let dx = -1; dx <= 1; dx++) {
        const x = cell.x + dx;
        const y = cell.y + dy;
        if (x >= 0 && x < GRID_SIZE && y >= 0 && y < GRID_SIZE) {
          blocked.add(y * GRID_SIZE + x);
        }
      }
    }
  }
}

/**
 * Generate one uniformly random legal fleet. Always succeeds — on the
 * (rare) dead-end the whole fleet restarts.
 */
export function generateFleet(): Ship[] {
  for (;;) {
    const ships: Ship[] = [];
    const blocked = new Set<number>();
    let stuck = false;

    for (const length of FLEET_LENGTHS) {
      let placed = false;
      for (let attempt = 0; attempt < ATTEMPTS_PER_SHIP; attempt++) {
        const horizontal = length === 1 ? true : cryptoInt(2) === 0;
        const maxX = horizontal ? GRID_SIZE - length : GRID_SIZE - 1;
        const maxY = horizontal ? GRID_SIZE - 1 : GRID_SIZE - length;
        const ship: Ship = {
          x: cryptoInt(maxX + 1),
          y: cryptoInt(maxY + 1),
          length,
          horizontal,
        };
        if (fits(ship, blocked)) {
          ships.push(ship);
          block(ship, blocked);
          placed = true;
          break;
        }
      }
      if (!placed) {
        stuck = true;
        break;
      }
    }

    if (!stuck) return ships;
  }
}
