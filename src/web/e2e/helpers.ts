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
