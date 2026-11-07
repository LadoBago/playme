import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// Node env (no jsdom) is enough for the current scope — we test pure
// helpers under apps/web/lib/ and apps/web/features/ that touch
// `window` / `document` via `vi.stubGlobal` rather than a real DOM.
// When component tests arrive, add jsdom + @testing-library/react and
// switch `environment` (or use environment-directives in individual
// test files).
//
// We deliberately exclude Next.js's build output and the Sentry config
// fixtures; the file glob is scoped to source dirs to keep the loader
// simple.
export default defineConfig({
  // Mirror the `@/` path alias from tsconfig so tested modules can keep the
  // project's import style (CLAUDE.md §6: no deep relative imports).
  resolve: {
    alias: { '@': fileURLToPath(new URL('.', import.meta.url)) },
  },
  test: {
    environment: 'node',
    include: [
      'lib/**/*.test.ts',
      'lib/**/*.test.tsx',
      'features/**/*.test.ts',
      'features/**/*.test.tsx',
    ],
  },
});
