import { test, expect } from '@playwright/test';

/**
 * The application shell renders and is navigable (V3-QA-001). Runs at every viewport, since the shell
 * is where the responsive nav rail collapses.
 */
test.describe('application shell', () => {
  test('renders the nav rail, header and a single main landmark', async ({ page }) => {
    await page.goto('/');

    await expect(page.getByRole('navigation', { name: /primary navigation/i })).toBeVisible();
    await expect(page.getByRole('main')).toHaveCount(1);
    // The skip link targets the main region (V3-NAV-002).
    const skip = page.getByRole('link', { name: /skip to main content/i });
    await expect(skip).toHaveAttribute('href', '#main-content');
  });

  test('the cockpit is the landing route and is marked current in the nav', async ({ page }) => {
    await page.goto('/');
    const cockpitLink = page
      .getByRole('navigation', { name: /primary navigation/i })
      .getByRole('link', { name: /decision cockpit/i });
    await expect(cockpitLink).toHaveAttribute('aria-current', 'page');
  });

  test('exposes no delete control anywhere in the shell (append-only, ADR 0006)', async ({ page }) => {
    await page.goto('/decisions');
    await expect(page.getByRole('heading', { name: /decision log/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /delete|remove/i })).toHaveCount(0);
  });
});
