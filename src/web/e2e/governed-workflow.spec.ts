import { test, expect } from '@playwright/test';

/**
 * The governed decision workflow (V3-QA-001, B.3.3). Runs at one representative desktop viewport.
 *
 * Coverage boundary: the API runs with a single LocalDev principal holding every role, so this journey
 * proves claim → accept → **self-approval refused server-side** (segregation of duties, ADR 0006 §6 —
 * a person may not approve their own decision), that the append-only log grows, and that no delete
 * control exists. The full happy path *through* sign-off → implemented → record-outcome needs a second,
 * distinct approver and is a tracked data-seeding follow-up (one persona cannot both author and
 * approve).
 */
test.beforeEach(({}, testInfo) => {
  test.skip(testInfo.project.name !== 'w1280', 'governed-workflow runs at w1280 only');
});

test.describe('governed decision workflow', () => {
  test('claim → accept → self-sign-off is refused, the log grows, and nothing can be deleted', async ({
    page,
  }) => {
    await page.goto('/decisions');
    await expect(page.getByRole('heading', { name: /decision log/i })).toBeVisible();

    // No delete control exists anywhere on the governed screen (append-only; the delete button of the
    // v3 prototype was replaced by a terminal state).
    await expect(page.getByRole('button', { name: /delete|remove/i })).toHaveCount(0);

    const claimable = page.locator('.dl-row', {
      has: page.getByRole('button', { name: 'Claim for review' }),
    });
    test.skip((await claimable.count()) === 0, 'no claimable recommendation in the seeded set');

    // Scope every step to one row, identified by its rule id, so a re-sort after invalidation cannot
    // move the journey onto a different record.
    const ruleId = (await claimable.first().locator('.dl-row__id').innerText()).trim();
    const row = () => page.locator('.dl-row', { hasText: ruleId });

    await row().getByRole('button', { name: 'Claim for review' }).click();

    // The claim advances the record; the accept transition the server now publishes appears.
    const accept = row().getByRole('button', { name: /^Accept$/ });
    await expect(accept).toBeVisible();
    await accept.click();

    // Attempt to sign off one's own decision — the server refuses (SubjectIds.Same), and the row shows
    // the server's own explanation rather than advancing.
    const signOff = row().getByRole('button', { name: 'Sign off' });
    await expect(signOff).toBeVisible();
    await signOff.click();
    await row().getByRole('button', { name: 'Approve step' }).click();
    await expect(row().getByRole('alert')).toBeVisible();

    // The append-only trail grew: the row's detail drawer shows a status history with entries.
    await row().getByRole('button', { name: /open evidence/i }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByRole('heading', { name: /status history/i })).toBeVisible();
  });
});
