import { describe, expect, it } from 'vitest';
import { generateFleet } from './fleet';
import { GRID_SIZE, shipCells } from './schema';

// The generator's output is re-validated server-side on commit, but a
// client that produces illegal fleets would strand the player in a
// reroll-and-reject loop — so the legality rules are asserted here over
// repeated samples (crypto randomness, so each run exercises new layouts).
describe('generateFleet', () => {
  const SAMPLES = 50;

  it('always produces the post-Soviet fleet composition inside the grid with no touching ships', () => {
    for (let run = 0; run < SAMPLES; run++) {
      const fleet = generateFleet();

      const lengths = fleet.map((s) => s.length).sort((a, b) => b - a);
      expect(lengths).toEqual([4, 3, 3, 2, 2, 2, 1, 1, 1, 1]);

      const allCells = fleet.map((ship) => shipCells(ship));
      for (const cells of allCells) {
        for (const cell of cells) {
          expect(cell.x).toBeGreaterThanOrEqual(0);
          expect(cell.x).toBeLessThan(GRID_SIZE);
          expect(cell.y).toBeGreaterThanOrEqual(0);
          expect(cell.y).toBeLessThan(GRID_SIZE);
        }
      }

      // No-touch rule: Chebyshev distance ≥ 2 between cells of distinct
      // ships (covers overlap and diagonal contact in one check).
      for (let i = 0; i < allCells.length; i++) {
        for (let j = i + 1; j < allCells.length; j++) {
          for (const a of allCells[i]!) {
            for (const b of allCells[j]!) {
              const distance = Math.max(Math.abs(a.x - b.x), Math.abs(a.y - b.y));
              expect(distance).toBeGreaterThanOrEqual(2);
            }
          }
        }
      }
    }
  });

  it('produces different layouts across rerolls', () => {
    const serialize = (fleet: ReturnType<typeof generateFleet>) =>
      JSON.stringify([...fleet].sort((a, b) => a.x - b.x || a.y - b.y));
    const layouts = new Set(Array.from({ length: 10 }, () => serialize(generateFleet())));
    expect(layouts.size).toBeGreaterThan(1);
  });
});
