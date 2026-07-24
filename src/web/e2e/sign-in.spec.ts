import { test, expect } from '@playwright/test';

test.beforeEach(({}, testInfo) => {
  test.skip(testInfo.project.name !== 'w1280', 'sign-in parity check runs at w1280 only');
});

/**
 * Sign-in journey (V3-AUTH-001 coverage, B.3.1 / B.6).
 *
 * The default E2E build runs the SPA in `local` mode against the LocalDev API, so there is deliberately
 * no sign-in gate — this asserts that local-mode parity (S4b / A.7.14): the app renders with no gate
 * and no account control, exactly as before sign-in existed.
 *
 * The real-token redirect/callback/token-attach journey (entra mode) is covered at the component level
 * in `src/components/layout/signin-flow.test.tsx` (Sign-in shown when anonymous, the gate redirects and
 * preserves `returnTo`, the account chip after sign-in, sign-out). A full end-to-end against a test
 * JWKS host — the backend has no seam to trust a test signing key today — is a tracked follow-up (B.6);
 * standing it up was out of scope for this slice.
 */
test.describe('sign-in — local-mode parity', () => {
  test('renders with no gate and no account control in local mode', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('main')).toBeVisible();

    await expect(page.getByText(/sign in to continue/i)).toHaveCount(0);
    await expect(page.getByRole('button', { name: /^sign in$/i })).toHaveCount(0);
    await expect(page.getByRole('button', { name: /^sign out$/i })).toHaveCount(0);
  });
});
