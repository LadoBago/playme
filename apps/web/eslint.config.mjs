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
      'lib/**/*.{ts,tsx}',
      'instrumentation.ts',
      'instrumentation-client.ts',
      'middleware.ts',
    ],
    languageOptions: {
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
  },
];
