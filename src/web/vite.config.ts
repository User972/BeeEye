import { fileURLToPath, URL } from 'node:url';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

export default defineConfig({
  plugins: [react()],
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
  build: {
    outDir: 'dist',
    sourcemap: false, // IP protection: no production source maps.
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./vitest.setup.ts'],
    css: true,
  },
});
