import { request } from '@playwright/test';

const API_URL = 'http://localhost:5080';

/**
 * Seeds the governed decision workflow before the suite runs.
 *
 * The sample data seeds the analytics, but recommendation *records* are generated on demand — a fresh
 * `/api/v1/decisions` is empty. This posts the generator once (idempotent: it reports `alreadyPresent`
 * on a re-run, and the Idempotency-Key makes a retry safe), so the governed-workflow, CSV-export and
 * decision-log journeys exercise real rows instead of skipping. Reads run under LocalDev (all roles),
 * and `recommendation.generate` is granted, so no token is needed.
 */
export default async function globalSetup(): Promise<void> {
  const ctx = await request.newContext();
  try {
    // The webServer is normally ready by now, but poll defensively so setup never races a cold API.
    for (let attempt = 0; attempt < 60; attempt += 1) {
      try {
        const health = await ctx.get(`${API_URL}/health/ready`);
        if (health.ok()) break;
      } catch {
        /* not up yet */
      }
      await new Promise((resolve) => setTimeout(resolve, 1000));
    }

    const res = await ctx.post(`${API_URL}/api/v1/recommendations/records/generate`, {
      headers: {
        'Idempotency-Key': 'e2e-seed-recommendations-0001',
        'Content-Type': 'application/json',
      },
      data: {},
    });
    if (!res.ok()) {
      throw new Error(`Recommendation generation failed: HTTP ${res.status()}`);
    }
  } finally {
    await ctx.dispose();
  }
}
