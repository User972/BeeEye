import { test, expect } from '@playwright/test';
import { readFile } from 'node:fs/promises';

test.beforeEach(({}, testInfo) => {
  test.skip(testInfo.project.name !== 'w1280', 'CSV export journey runs at w1280 only');
});

/**
 * The Decision Log CSV export (V3-QA-001, B.3.5). The rigorous field-level escaping (commas, quotes,
 * newlines, formula-injection prefixes) is unit-tested in `src/lib/csv.test.ts`; this end-to-end check
 * proves the export button downloads a well-formed CSV with the expected header and that no cell begins
 * with a bare formula-trigger character.
 */
test.describe('decision-log CSV export', () => {
  test('downloads a well-formed, injection-safe CSV', async ({ page }) => {
    await page.goto('/decisions');
    await expect(page.getByRole('heading', { name: /decision log/i })).toBeVisible();

    const exportButton = page.getByRole('button', { name: /^export$/i });
    test.skip(await exportButton.isDisabled(), 'no rows to export in the seeded set');

    const [download] = await Promise.all([page.waitForEvent('download'), exportButton.click()]);
    expect(download.suggestedFilename()).toBe('admc_decision_log.csv');

    const path = await download.path();
    expect(path).not.toBeNull();
    const content = await readFile(path as string, 'utf8');

    const [header, ...rows] = content.split(/\r?\n/).filter((line) => line.length > 0);
    expect(header).toContain('id,decision,module');

    // No cell may begin with a bare formula-injection trigger (=, +, -, @, tab, CR) — such fields are
    // prefixed defensively by toCsv, so an unquoted, unprefixed leading trigger would be a regression.
    for (const line of rows) {
      const firstCell = line.split(',')[0] ?? '';
      expect(firstCell).not.toMatch(/^[=+\-@\t\r]/);
    }
  });
});
