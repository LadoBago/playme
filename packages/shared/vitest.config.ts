import { defineConfig } from 'vitest/config';

// Pure-TS package: Node env is the right default. Tests live alongside
// source files as *.test.ts (closer to the unit under test than a parallel
// __tests__ tree; matches the catalog/games.test.ts convention).
export default defineConfig({
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
});
