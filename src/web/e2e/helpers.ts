import { expect } from '@playwright/test';
import type { Locator, Page } from '@playwright/test';

/**
 * The routed screens, mirroring `src/config/navigation.ts`. Kept local so the E2E suite needs no `@`
 * path alias in its own tsconfig. `heading` matches the screen's `<h1>` once it has rendered.
 */
export const ROUTES: ReadonlyArray<{ id: string; path: string; heading: RegExp }> = [
  { id: 'executive-cockpit', path: '/', heading: /decision cockpit/i },
  { id: 'order-optimisation', path: '/order-optimisation', heading: /order optimisation/i },
  { id: 'sales-forecasting', path: '/forecasting', heading: /forecast accuracy/i },
  { id: 'configuration-demand', path: '/configuration-demand', heading: /configuration insights/i },
  { id: 'procurement', path: '/procurement', heading: /procurement optimisation/i },
  { id: 'inventory-intelligence', path: '/inventory', heading: /inventory aging & overstock/i },
  { id: 'after-sales', path: '/after-sales', heading: /sales .* service correlation/i },
  { id: 'spare-parts', path: '/spare-parts', heading: /spare parts prediction/i },
  { id: 'decision-log', path: '/decisions', heading: /decision log/i },
  { id: 'platform-settings', path: '/settings', heading: /settings/i },
  { id: 'data-management', path: '/data', heading: /data management/i },
];

/**
 * Freezes the UI so a screenshot is byte-stable (B.4):
 * - kills every transition/animation and hides the text caret;
 * - waits for the Material Symbols icon font, which `main.tsx` signals by adding `icons-ready` to
 *   `<html>`, so icons render as glyphs rather than raw ligature names.
 */
export async function stabilise(page: Page): Promise<void> {
  await page.addStyleTag({
    content:
      '*,*::before,*::after{transition:none!important;animation:none!important;caret-color:transparent!important;scroll-behavior:auto!important}',
  });
  await page
    .waitForFunction(() => document.documentElement.classList.contains('icons-ready'), null, {
      timeout: 10_000,
    })
    .catch(() => {
      /* offline/font blocked — text labels still carry meaning; snapshot proceeds */
    });
}

/**
 * Opens the global explainability drawer on the current screen and returns it.
 *
 * Some screens expose the "Why this…" trigger directly (e.g. Forecast Accuracy); others reveal it
 * inside a row detail (e.g. Inventory — "click a row for the full risk breakdown and recommendation"),
 * so this clicks the first data row when the trigger is not already present. The returned dialog is
 * scoped by its feedback section, so a stacked row-detail drawer is never mistaken for the explanation.
 */
export async function openExplainabilityDrawer(page: Page): Promise<Locator> {
  const trigger = () => page.getByRole('button', { name: /why this/i }).first();
  if (!(await trigger().isVisible().catch(() => false))) {
    const firstRow = page.getByRole('row').nth(1); // nth(1): skip the header row
    if ((await firstRow.count()) > 0) {
      await firstRow.click();
    }
  }
  await trigger().click();
  const dialog = page.getByRole('dialog').filter({ hasText: /was this useful/i });
  await expect(dialog).toBeVisible();
  return dialog;
}

/** Opens the mobile navigation rail if it is collapsed behind the header toggle (narrow viewports). */
export async function revealNav(page: Page): Promise<void> {
  const toggle = page.getByRole('button', { name: /open navigation menu/i });
  if (await toggle.isVisible().catch(() => false)) {
    await toggle.click();
  }
}

/** Toggles the app theme the same way the header control does — by stamping `data-theme` on `<html>`. */
export async function setTheme(page: Page, theme: 'light' | 'dark'): Promise<void> {
  await page.evaluate((value) => {
    document.documentElement.dataset['theme'] = value;
  }, theme);
}

/**
 * Regions whose text is live or wall-clock-derived and must be masked out of visual snapshots.
 *
 * The app is largely deterministic (figures anchor to the seeded dataset, dates resolve through
 * `IClock`), so this list is deliberately small. If a baseline churns on an unmasked region, add its
 * selector here — this is the single knob for visual stability.
 */
export function volatileMasks(page: Page): Locator[] {
  return [page.locator('time'), page.locator('[data-volatile]')];
}
