import base from '@playme/config/eslint.base.mjs';

export default [
  ...base,
  {
    // vitest.config.ts is a build-time config, not app code — the
    // type-aware parserOptions block below scopes Project Service to
    // app/features/lib only, so linting vitest.config.ts would trip
    // `await-thenable` (needs type info that isn't generated for it).
    ignores: ['.next/**', 'next-env.d.ts', 'node_modules/**', 'vitest.config.ts'],
  },
  // Type-aware linting for app source.
  {
    files: [
      'app/**/*.{ts,tsx}',
      'features/**/*.{ts,tsx}',
      'components/**/*.{ts,tsx}',
      'lib/**/*.{ts,tsx}',
      'instrumentation.ts',
      'instrumentation-client.ts',
      'proxy.ts',
    ],
    languageOptions: {
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
  },
  // The PWA service worker runs in ServiceWorkerGlobalScope, not the
  // browser window — it needs the worker-specific globals and a few
  // Fetch API globals that `js.configs.recommended` doesn't include by
  // default. Scoped narrowly to public/sw.js so the rest of public/
  // (static assets) stays unlinted.
  {
    files: ['public/sw.js'],
    languageOptions: {
      globals: {
        self: 'readonly',
        caches: 'readonly',
        clients: 'readonly',
        fetch: 'readonly',
        Response: 'readonly',
        Request: 'readonly',
        URL: 'readonly',
      },
    },
  },
];
