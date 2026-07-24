import { test, expect } from '@playwright/test';

test.beforeEach(({}, testInfo) => {
  test.skip(testInfo.project.name !== 'w1280', 'cockpit journey runs at w1280 only');
});

/**
 * The Executive Decision Cockpit loads from seeded data (V3-QA-001, B.3.2). Figures are deterministic
 * (IClock + seeded dataset), so this asserts the data-backed state rather than an error/empty shell,
 * and that synthetic-demo content carries its AI/demo label.
 */
test.describe('executive decision cockpit', () => {
  test('loads the seeded decision set, labelled, with no error state', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('heading', { name: /decision cockpit/i })).toBeVisible();

    // Data-backed, not the error state.
    await expect(page.getByText(/something went wrong/i)).toHaveCount(0);

    // The AI / synthetic-demo labelling is present (UC6/UC7 are a clearly-labelled demo dataset).
    await expect(page.locator('.ai-label').first()).toBeVisible();
  });
});
