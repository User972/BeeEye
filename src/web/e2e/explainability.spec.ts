import { test, expect } from '@playwright/test';

test.beforeEach(({}, testInfo) => {
  test.skip(testInfo.project.name !== 'w1280', 'explainability journey runs at w1280 only');
});

/**
 * The global explainability drawer (V3-QA-001, B.3.4): opens for the correct subject on several
 * screens, shows the retraining caveat, and round-trips feedback.
 */
const SCREENS = ['/inventory', '/forecasting', '/order-optimisation'] as const;

test.describe('explainability drawer', () => {
  for (const path of SCREENS) {
    test(`opens on ${path} and shows the retraining caveat`, async ({ page }) => {
      await page.goto(path);
      const trigger = page.getByRole('button', { name: /why this/i }).first();
      await expect(trigger).toBeVisible();
      await trigger.click();

      const dialog = page.getByRole('dialog');
      await expect(dialog).toBeVisible();
      // The caveat makes plain that feedback retrains nothing (V3-DS-006).
      await expect(dialog.getByText(/retrain|analytics platform only/i)).toBeVisible();
    });
  }

  test('feedback round-trips to the server and is confirmed', async ({ page }) => {
    await page.goto('/inventory');
    await page.getByRole('button', { name: /why this/i }).first().click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await dialog.getByRole('button', { name: 'Useful' }).click();
    await expect(dialog.getByText(/your feedback was recorded/i)).toBeVisible();
  });
});
