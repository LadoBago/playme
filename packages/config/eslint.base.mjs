// @ts-check
import js from '@eslint/js';
import tseslint from 'typescript-eslint';
import securityPlugin from 'eslint-plugin-security';

/**
 * Shared base ESLint config for PlayMe TS workspaces.
 *
 * Per CLAUDE.md §5.10, `eslint-plugin-security` is enabled in the web
 * workspace; we apply it project-wide so packages also get scanned.
 *
 * Type-aware rules are restricted to **app source** (`src/**`, `app/**`,
 * `features/**`). Loose JS/MJS/CJS config files at workspace roots are
 * linted with the non-type-aware ruleset so they don't need to be in a
 * tsconfig project.
 */
export default tseslint.config(
  {
    ignores: [
      '**/node_modules/**',
      '**/dist/**',
      '**/.next/**',
      '**/.turbo/**',
      '**/coverage/**',
      '**/*.generated.ts',
      'packages/shared/src/api/generated/**',
    ],
  },
  // Baseline rules for every file.
  js.configs.recommended,
  securityPlugin.configs.recommended,

  // Type-aware rules for TS/TSX *only*. Workspace eslint.config.mjs adds
  // `parserOptions.projectService: true` (and tsconfigRootDir) so the
  // type service can resolve files.
  {
    files: ['**/*.{ts,tsx}'],
    extends: [...tseslint.configs.recommendedTypeChecked],
    rules: {
      // CLAUDE.md §8 — no `any`; use `unknown` and narrow.
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/no-unsafe-assignment': 'error',
      '@typescript-eslint/no-unsafe-call': 'error',
      '@typescript-eslint/no-unsafe-member-access': 'error',
      '@typescript-eslint/no-floating-promises': 'error',
      '@typescript-eslint/no-misused-promises': 'error',
      '@typescript-eslint/consistent-type-imports': [
        'error',
        { prefer: 'type-imports', fixStyle: 'separate-type-imports' },
      ],
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_', caughtErrorsIgnorePattern: '^_' },
      ],
    },
  },
);
