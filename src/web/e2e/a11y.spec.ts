import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { ROUTES, setTheme, stabilise } from './helpers';

/**
 * Route-level accessibility (V3-QA-003 / R-11). Runs in a real browser, so — unlike the component-level
 * axe layer in jsdom — colour-contrast is actually computed. Both themes are scanned because contrast
 * differs between them. The bar is **zero serious/critical** violations. Any known, accepted violation
 * would be allow-listed here individually with a comment and a tracking note — never a blanket disable.
 *
 * Restricted to a representative mobile + desktop viewport to keep the matrix sane; the visual suite
 * carries the full 7-viewport coverage.
 */
const A11Y_VIEWPORTS = new Set(['w390', 'w1280']);
const THEMES = ['light', 'dark'] as const;

test.beforeEach(({}, testInfo) => {
  test.skip(!A11Y_VIEWPORTS.has(testInfo.project.name), 'a11y runs at w390 + w1280 only');
});

/** Fails with the offending rule ids in the message so a CI failure is self-explaining. */
async function expectNoSeriousViolations(scan: AxeBuilder): Promise<void> {
  const results = await scan.withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze();
  const serious = results.violations.filter(
    (v) => v.impact === 'serious' || v.impact === 'critical',
  );
  expect(serious.map((v) => `${v.id} (${v.impact})`)).toEqual([]);
}

test.describe('accessibility — every route, both themes', () => {
  for (const route of ROUTES) {
    for (const theme of THEMES) {
      test(`${route.id} — ${theme}`, async ({ page }) => {
        await page.goto(route.path);
        await expect(page.getByRole('heading', { name: route.heading }).first()).toBeVisible();
        await setTheme(page, theme);
        await stabilise(page);
        await expectNoSeriousViolations(new AxeBuilder({ page }));
      });
    }
  }
});

test.describe('accessibility — key modal states', () => {
  test('explainability drawer (open) has no serious/critical violations', async ({ page }) => {
    await page.goto('/inventory');
    const trigger = page.getByRole('button', { name: /why this/i }).first();
    await trigger.click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await stabilise(page);
    // Scope the scan to the open dialog — its focus trap and 11 sections are the a11y-critical surface.
    await expectNoSeriousViolations(new AxeBuilder({ page }).include('[role="dialog"]'));
  });

  test('decision-log detail drawer (open) has no serious/critical violations', async ({ page }) => {
    await page.goto('/decisions');
    await expect(page.getByRole('heading', { name: /decision log/i })).toBeVisible();
    const open = page.getByRole('button', { name: /open evidence/i }).first();
    await open.click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await stabilise(page);
    await expectNoSeriousViolations(new AxeBuilder({ page }).include('[role="dialog"]'));
  });
});
