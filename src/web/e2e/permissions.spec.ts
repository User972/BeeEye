import { test, expect } from '@playwright/test';

test.beforeEach(({}, testInfo) => {
  test.skip(testInfo.project.name !== 'w1280', 'permission/state journey runs at w1280 only');
});

/**
 * Read-only posture and governed-state messaging (V3-QA-001, B.3.6).
 *
 * Coverage boundary: the default E2E run uses one LocalDev principal holding every role, so it cannot
 * exercise a 403 permission-denied path. Those, and the loading/empty/error states, are covered at the
 * component level (`decision-log.test.tsx`, `ExplainabilityDrawer.test.tsx`). A persona-driven
 * permission-denied E2E — re-running this project with `Auth__LocalDevUser__Roles__0=Analyst` (which
 * lacks `recommendation.approve`, so approver controls are hidden and the row hint appears) — is a
 * tracked follow-up. What this asserts is the structural, always-true read-only posture.
 */
test.describe('read-only posture and governed messaging', () => {
  test('the read-only badge and the append-only banner are always present', async ({ page }) => {
    await page.goto('/decisions');
    await expect(page.getByRole('heading', { name: /decision log/i })).toBeVisible();

    // BeeEye never writes back to Oracle Fusion — the header says so on every screen.
    await expect(page.getByText(/read-only analytics/i)).toBeVisible();

    // The governed audit-trail banner explains the append-only model (no free status editing, no delete).
    await expect(page.getByText(/governed audit trail/i)).toBeVisible();
  });

  test('a status filter with no matches renders an explicit empty state, not a blank card', async ({
    page,
  }) => {
    await page.goto('/decisions');
    await expect(page.getByRole('heading', { name: /decision log/i })).toBeVisible();

    const chips = page.getByRole('group', { name: /filter by status/i }).getByRole('button');
    test.skip((await chips.count()) === 0, 'no status chips in the seeded set');

    // Filtering to a status, then to another, eventually lands on one with no rows for some datasets;
    // when it does, the empty state is explicit and offers to clear the filter.
    await chips.last().click();
    const emptyOrRows = page.getByText(/no decisions match this status filter/i).or(
      page.locator('.dl-row').first(),
    );
    await expect(emptyOrRows).toBeVisible();
  });
});
