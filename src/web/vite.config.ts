import { fileURLToPath, URL } from 'node:url';
import react from '@vitejs/plugin-react';
import { loadEnv } from 'vite';
import type { Plugin } from 'vite';
import { defineConfig } from 'vitest/config';
import { resolveAuthConfig } from './src/lib/auth/config';

/**
 * Fails a production `vite build` when the app is meant for Entra but is missing the configuration to
 * sign anyone in (ADR 0008, S4b / A.8). A deployment must never boot into accidental anonymous mode.
 * It reuses the runtime `resolveAuthConfig`, so build-time and runtime enforcement cannot diverge.
 * `local` mode (the default when no client id is set) passes, so CI and dev builds are unaffected.
 */
function authBuildGuard(): Plugin {
  return {
    name: 'beeeye-auth-build-guard',
    apply: 'build',
    config(_config, { mode }) {
      const env = loadEnv(mode, process.cwd(), 'VITE_');
      try {
        resolveAuthConfig(
          {
            VITE_AUTH_MODE: env.VITE_AUTH_MODE,
            VITE_AAD_CLIENT_ID: env.VITE_AAD_CLIENT_ID,
            VITE_AAD_AUTHORITY: env.VITE_AAD_AUTHORITY,
            VITE_AAD_API_SCOPE: env.VITE_AAD_API_SCOPE,
            VITE_AAD_REDIRECT_URI: env.VITE_AAD_REDIRECT_URI,
          },
          'https://placeholder.invalid',
        );
      } catch (error) {
        throw new Error(
          `[beeeye-auth-build-guard] ${error instanceof Error ? error.message : String(error)}`,
        );
      }
    },
  };
}

export default defineConfig({
  plugins: [react(), authBuildGuard()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    proxy: {
      // Dev-only: proxy API calls to the .NET host so the SPA and API share an origin.
      '/api': { target: 'http://localhost:5080', changeOrigin: true },
      '/health': { target: 'http://localhost:5080', changeOrigin: true },
    },
  },
  preview: {
    // `vite preview` serves the built app for the Playwright E2E suite (S12). It proxies to the API
    // exactly as the dev server does, so the E2E SPA is same-origin with the API and needs no CORS
    // change and no VITE_API_BASE_URL.
    port: 4173,
    proxy: {
      '/api': { target: 'http://localhost:5080', changeOrigin: true },
      '/health': { target: 'http://localhost:5080', changeOrigin: true },
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: false, // IP protection: no production source maps.
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./vitest.setup.ts'],
    css: true,
    // Vitest owns the unit/component suite under src/. The Playwright E2E specs live in e2e/ and use a
    // different runner, so they are excluded from vitest discovery (they would otherwise be collected
    // by the default *.spec.ts glob and fail).
    include: ['src/**/*.{test,spec}.{ts,tsx}'],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'html', 'lcov'],
      // Generated, bootstrap, config, type-only and test files are not meaningful coverage targets.
      exclude: [
        'src/lib/api/schema.d.ts',
        'src/main.tsx',
        'src/vite-env.d.ts',
        '**/*.test.{ts,tsx}',
        '**/*.config.ts',
        'vitest.setup.ts',
        'e2e/**',
        'dist/**',
        'node_modules/**',
      ],
      // A floor the current suite already clears (measured: stmts 87.4 · branch 81.9 · funcs 75.5 ·
      // lines 87.4), set just below so the gate holds without flaking and can only ratchet upward
      // (S12 / V3-QA-004). Raise these as coverage grows; never lower them.
      thresholds: {
        lines: 86,
        functions: 74,
        branches: 80,
        statements: 86,
      },
    },
  },
});
