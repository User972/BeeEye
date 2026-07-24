import js from '@eslint/js';
import globals from 'globals';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import tseslint from 'typescript-eslint';

/**
 * Flat ESLint config for the BeeEye web app (React 19 + TypeScript, strict).
 * Type-aware linting (via the project service) is scoped to the app's
 * .ts/.tsx sources; JS config files are linted without type information.
 */
export default tseslint.config(
  {
    // Build output, deps, generated API types and test artefacts are not linted.
    ignores: [
      'dist',
      'node_modules',
      'src/lib/api/schema.d.ts',
      'coverage',
      'test-results',
      'playwright-report',
    ],
  },
  js.configs.recommended,
  {
    files: ['**/*.{ts,tsx}'],
    extends: [...tseslint.configs.recommendedTypeChecked],
    languageOptions: {
      ecmaVersion: 2023,
      globals: globals.browser,
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    plugins: {
      'react-hooks': reactHooks,
      'react-refresh': reactRefresh,
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
    },
  },
  {
    // Test files run under jsdom/node and don't need the floating-promises guard.
    files: ['**/*.test.{ts,tsx}', 'vitest.setup.ts'],
    languageOptions: { globals: { ...globals.node } },
    rules: {
      '@typescript-eslint/no-floating-promises': 'off',
    },
  },
  {
    // JS config files (this file, vite/vitest config) are not in the TS project:
    // lint them without type-aware rules.
    files: ['**/*.{js,cjs,mjs}'],
    extends: [tseslint.configs.disableTypeChecked],
    languageOptions: { globals: { ...globals.node } },
  },
  {
    // The E2E suite and the Playwright config live outside the app's tsconfig project (Playwright
    // compiles and type-checks them itself via e2e/tsconfig.json). Lint them without type-aware rules
    // and with node globals, so `eslint .` does not error on files the project service does not own.
    files: ['e2e/**/*.ts', 'playwright.config.ts'],
    extends: [tseslint.configs.disableTypeChecked],
    languageOptions: {
      globals: { ...globals.node },
      parserOptions: { projectService: false, project: false },
    },
    // `({}, testInfo) => …` is Playwright's idiomatic signature for reaching testInfo without fixtures.
    rules: { 'no-empty-pattern': 'off' },
  },
);
