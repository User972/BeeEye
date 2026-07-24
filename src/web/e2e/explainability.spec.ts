import { test, expect } from '@playwright/test';
import { openExplainabilityDrawer } from './helpers';

test.beforeEach(({}, testInfo) => {
  test.skip(testInfo.project.name !== 'w1280', 'explainability journey runs at w1280 only');
});

/**
 * The global explainability drawer (V3-QA-001, B.3.4): opens for the correct subject on several
 * screens, shows the retraining caveat, and round-trips feedback.
 */
// Direct-trigger screens (the "Why this…" control is on the page, not behind an async row-detail
// load), so the journey is deterministic. That the drawer is wired into every screen — including the
// row-detail ones like Inventory — is proven at the component level in explainability-wiring.test.tsx.
const SCREENS = ['/forecasting', '/order-optimisation', '/procurement'] as const;

test.describe('explainability drawer', () => {
  for (const path of SCREENS) {
    test(`opens on ${path} and shows the retraining caveat`, async ({ page }) => {
      await page.goto(path);
      const dialog = await openExplainabilityDrawer(page);
      // The caveat makes plain that feedback retrains nothing (V3-DS-006).
      await expect(dialog.getByText(/retrain|analytics platform only|does not change/i)).toBeVisible();
    });
  }

  test('feedback round-trips to the server and is confirmed', async ({ page }) => {
    // A direct-trigger screen keeps this focused on the feedback write, not the reveal mechanics.
    await page.goto('/forecasting');
    const dialog = await openExplainabilityDrawer(page);
    await dialog.getByRole('button', { name: 'Useful' }).click();
    await expect(dialog.getByText(/your feedback was recorded/i)).toBeVisible();
  });
});
