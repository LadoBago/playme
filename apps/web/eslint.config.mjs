import base from '@playme/config/eslint.base.mjs';

export default [
  ...base,
  {
    ignores: ['.next/**', 'next-env.d.ts', 'node_modules/**'],
  },
  // Type-aware linting for app source.
  {
    files: [
      'app/**/*.{ts,tsx}',
      'features/**/*.{ts,tsx}',
      'lib/**/*.{ts,tsx}',
      'instrumentation.ts',
      'instrumentation-client.ts',
    ],
    languageOptions: {
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
  },
];
