import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright configuration for the BeeEye E2E, visual-regression and accessibility suites (S12).
 *
 * Determinism (V3-QA / R-09 / R-11): the app resolves time through `IClock` and anchors analysis to
 * the seeded dataset, not the wall clock, so figures are stable. The API runs in the Development /
 * LocalDev posture (all roles, no tenant) and the SPA is served by `vite preview` in `local` mode, so
 * every journey is deterministic and needs no Entra tenant. `preview.proxy` (vite.config.ts) makes the
 * SPA same-origin with the API, so there is no CORS step. Baselines are platform-pinned — generate them
 * in the same container CI uses (the Playwright image), never on a developer's macOS/Windows.
 */

const isCI = !!process.env['CI'];
const SPA_URL = 'http://localhost:4173';
const API_URL = 'http://localhost:5080';

/** The seven viewports V3-QA-002 fixes (widths in px). Heights are conventional for each class. */
const VIEWPORTS = [
  { name: 'w360', width: 360, height: 780 },
  { name: 'w390', width: 390, height: 844 },
  { name: 'w768', width: 768, height: 1024 },
  { name: 'w1024', width: 1024, height: 768 },
  { name: 'w1280', width: 1280, height: 800 },
  { name: 'w1440', width: 1440, height: 900 },
  { name: 'w1920', width: 1920, height: 1080 },
] as const;

export default defineConfig({
  testDir: './e2e',
  // Generates the recommendation records once before the suite, so the governed-workflow / CSV /
  // decision-log journeys run against real rows instead of skipping on an empty log.
  globalSetup: './e2e/global-setup.ts',
  // Baselines are committed under e2e/__screenshots__ and are OS/browser-pinned by the {platform}
  // segment, so a macOS/Windows render never churns the Linux-CI baseline (B.4).
  snapshotPathTemplate: 'e2e/__screenshots__/{testFilePath}/{arg}-{projectName}-{platform}{ext}',
  // No arbitrary sleeps anywhere in the suite; web-first assertions carry their own waiting (B.7).
  fullyParallel: true,
  forbidOnly: isCI,
  // At most one retry, and only in CI; a test that passes only on retry is quarantined, not left green.
  retries: isCI ? 1 : 0,
  reporter: isCI
    ? [['github'], ['html', { open: 'never' }], ['list']]
    : [['html', { open: 'never' }], ['list']],
  outputDir: 'test-results',
  timeout: 30_000,
  expect: {
    timeout: 10_000,
    // A small tolerance for sub-pixel/font-hinting differences; anything larger fails the visual gate.
    toHaveScreenshot: { maxDiffPixelRatio: 0.02, animations: 'disabled' },
  },
  use: {
    baseURL: SPA_URL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    // Reduced motion removes transition churn from screenshots and reflects a real user preference.
    contextOptions: { reducedMotion: 'reduce' },
  },
  projects: VIEWPORTS.map((viewport) => ({
    name: viewport.name,
    use: {
      ...devices['Desktop Chrome'],
      viewport: { width: viewport.width, height: viewport.height },
    },
  })),
  webServer: [
    {
      // The API in LocalDev posture: all roles, reads open, no tenant. `/health/ready` reports actual
      // DB connectivity, so waiting on it guarantees the seeded data is present before any test runs.
      command: 'dotnet run --project ../api/BeeEye.Api -c Release',
      url: `${API_URL}/health/ready`,
      timeout: 180_000,
      reuseExistingServer: !isCI,
      stdout: 'pipe',
      stderr: 'pipe',
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        ASPNETCORE_URLS: API_URL,
      },
    },
    {
      // Build in `local` mode (the default) and serve it; `vite preview` proxies /api to the API host.
      command: 'npm run build && npm run preview',
      url: SPA_URL,
      timeout: 180_000,
      reuseExistingServer: !isCI,
      stdout: 'pipe',
      stderr: 'pipe',
    },
  ],
});
