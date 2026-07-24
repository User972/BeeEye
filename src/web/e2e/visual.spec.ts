import { test, expect } from '@playwright/test';
import { ROUTES, setTheme, stabilise, volatileMasks, waitForStableLayout } from './helpers';

/**
 * Visual regression (V3-QA-002 / R-09): every screen × 7 viewports × light + dark. The viewport comes
 * from the Playwright project, so this file itself only loops routes × themes; running it under all
 * seven projects produces the full 7-viewport matrix.
 *
 * Stability (B.4): animations disabled (config + `stabilise`), reduced motion (config context),
 * icon font settled before the shot, and volatile regions masked. Baselines are platform-pinned —
 * generate them in the Playwright container CI uses, never on a developer machine:
 *   npx playwright test e2e/visual.spec.ts --update-snapshots
 * An unreviewed pixel change fails CI, mirroring the OpenAPI drift-gate philosophy.
 */
const THEMES = ['light', 'dark'] as const;

// Tagged @visual so CI can gate the functional/a11y suites independently of the pixel baselines
// (which are platform-pinned and bootstrapped separately — see the web README).
test.describe('visual regression @visual — every screen, both themes', () => {
  for (const route of ROUTES) {
    for (const theme of THEMES) {
      test(`${route.id} — ${theme}`, async ({ page }) => {
        await page.goto(route.path);
        await expect(page.getByRole('heading', { name: route.heading }).first()).toBeVisible();
        await setTheme(page, theme);
        await stabilise(page);
        // Pin the capture to the settled page height so async-loaded tables can't shrink the shot on
        // one run and grow it on another (a size mismatch no mask absorbs — see waitForStableLayout).
        await waitForStableLayout(page);

        await expect(page).toHaveScreenshot(`${route.id}-${theme}.png`, {
          fullPage: true,
          mask: volatileMasks(page),
        });
      });
    }
  }
});
