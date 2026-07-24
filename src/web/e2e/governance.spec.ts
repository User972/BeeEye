import { test, expect } from '@playwright/test';

test.beforeEach(({}, testInfo) => {
  test.skip(testInfo.project.name !== 'w1280', 'governance journeys run at w1280 only');
});

/**
 * The three S7 governance screens load from the real, seeded backend (V3-GOV-008/009/010). Every value
 * is deterministic (seeded dataset + engine constants), so these assert the data-backed state — not an
 * error or empty shell — and that synthetic-demo provenance is labelled, never presented as real.
 */
test.describe('Data Health (V3-GOV-008)', () => {
  test('shows the governed sources, the score and the demo labelling', async ({ page }) => {
    await page.goto('/data');
    await expect(page.getByRole('heading', { name: /data health/i })).toBeVisible();

    await expect(page.getByText(/something went wrong/i)).toHaveCount(0);

    // The two real sources are present, and the synthetic sources carry the demo label.
    await expect(page.getByRole('cell', { name: 'Sales history' })).toBeVisible();
    await expect(page.getByRole('cell', { name: /vehicle mileage & warranty/i })).toBeVisible();
    await expect(page.locator('.ai-label').first()).toBeVisible();

    // A blocked source is a first-class state, visibly distinct.
    await expect(page.getByText('Blocked').first()).toBeVisible();
  });
});

test.describe('Model & Data Lineage (V3-GOV-009)', () => {
  test('shows the pipeline and the per-metric provenance, labelled', async ({ page }) => {
    await page.goto('/lineage');
    await expect(page.getByRole('heading', { name: /model .* data lineage/i })).toBeVisible();

    await expect(page.getByText(/something went wrong/i)).toHaveCount(0);

    await expect(page.getByText(/no write-back in this phase/i)).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Recommended order mix' })).toBeVisible();
    // The synthetic-demo metrics (UC4/UC6/UC7) carry the demo label.
    await expect(page.locator('.ai-label').first()).toBeVisible();
  });
});

test.describe('Settings (V3-GOV-010)', () => {
  test('shows the read-only risk configuration', async ({ page }) => {
    await page.goto('/settings');
    await expect(page.getByRole('heading', { name: /^settings$/i })).toBeVisible();

    await expect(page.getByText(/something went wrong/i)).toHaveCount(0);

    await expect(page.getByText(/current configuration · read-only/i).first()).toBeVisible();
    await expect(page.getByText('Risk-factor weights')).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Stock cover' })).toBeVisible();
  });
});
