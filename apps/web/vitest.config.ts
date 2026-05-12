import { defineConfig } from 'vitest/config';

// Node env (no jsdom) is enough for the current scope — we test pure
// helpers under apps/web/lib/. When component tests arrive, add jsdom +
// @testing-library/react and switch `environment` (or use environment-
// directives in individual test files).
//
// We deliberately exclude Next.js's build output and the Sentry config
// fixtures; the file glob is scoped to `lib/` for now to keep the loader
// simple.
export default defineConfig({
  test: {
    environment: 'node',
    include: ['lib/**/*.test.ts', 'lib/**/*.test.tsx'],
  },
});
