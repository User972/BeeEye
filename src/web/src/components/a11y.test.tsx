import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { axe } from 'vitest-axe';
import type { Result } from 'axe-core';
import { AiLabel } from '@/components/ui/AiLabel';
import { Drawer } from '@/components/ui/Drawer';
import { PageHeader } from '@/components/ui/PageHeader';
import { EmptyState, ErrorState, LoadingState } from '@/components/ui/states';
import { ExplainabilityDrawer } from '@/components/domain/ExplainabilityDrawer';
import type { CurrentUser } from '@/lib/api/identity';
import type { Explanation, ExplanationResponse } from '@/lib/api/explainability';

// ---------------------------------------------------------------------------
// Component-level accessibility (S12 / V3-QA-003).
//
// Runs in jsdom, so this layer catches structural/ARIA defects; colour-contrast (which needs real
// layout) is verified at the route level in a real browser (see e2e/a11y.spec.ts). Both themes are
// still toggled here so a theme-specific structural regression cannot slip through. We assert zero
// *serious/critical* violations — the bar the slice sets.
// ---------------------------------------------------------------------------

const THEMES = ['light', 'dark'] as const;

/** The serious/critical violations only — the bar this slice enforces. */
async function seriousViolations(container: HTMLElement): Promise<Result[]> {
  // `color-contrast` needs real layout (a canvas) that jsdom does not provide, so it only produces
  // noise here; contrast is audited authoritatively in a real browser at the route level
  // (e2e/a11y.spec.ts). Disable it at the component level rather than let it spam or false-positive.
  const results = await axe(container, { rules: { 'color-contrast': { enabled: false } } });
  return results.violations.filter((v) => v.impact === 'serious' || v.impact === 'critical');
}

function withTheme(theme: (typeof THEMES)[number]): void {
  document.documentElement.dataset['theme'] = theme;
}

afterEach(() => {
  delete document.documentElement.dataset['theme'];
});

describe('design-system primitives have no serious/critical a11y violations (both themes)', () => {
  for (const theme of THEMES) {
    it(`AiLabel — ${theme}`, async () => {
      withTheme(theme);
      const { container } = render(
        <div>
          <AiLabel kind="recommendation" />
          <AiLabel kind="demo" />
        </div>,
      );
      expect(await seriousViolations(container)).toEqual([]);
    });

    it(`loading / empty / error states — ${theme}`, async () => {
      withTheme(theme);
      const { container } = render(
        <div>
          <LoadingState label="Loading…" />
          <EmptyState title="Nothing here" message="No rows match." />
          <ErrorState title="It broke" message="Try again." onRetry={() => undefined} />
        </div>,
      );
      expect(await seriousViolations(container)).toEqual([]);
    });

    it(`PageHeader — ${theme}`, async () => {
      withTheme(theme);
      const { container } = render(
        <PageHeader
          title="Decision Log"
          summary="What was advised, who decided, what they changed."
          wireframed
          meta={[{ label: 'Record model', value: 'Append-only (ADR 0006)' }]}
        />,
      );
      expect(await seriousViolations(container)).toEqual([]);
    });

    it(`Drawer (focus trap, open) — ${theme}`, async () => {
      withTheme(theme);
      const { container } = render(
        <Drawer open title="A panel" onClose={() => undefined}>
          <p>Body content</p>
          <button type="button">An action</button>
        </Drawer>,
      );
      // The dialog mounts into a portal-free overlay in the same container.
      expect(await seriousViolations(container)).toEqual([]);
    });
  }
});

// ---------------------------------------------------------------------------
// ExplainabilityDrawer — populated, both themes
// ---------------------------------------------------------------------------

const explanation: Explanation = {
  title: 'ES 350 ZX',
  module: 'Inventory Intelligence',
  label: 'recommendation',
  recommendation: 'Transfer stock from Riyadh to Jeddah.',
  impacts: [{ label: 'Holding cost accrued', value: 'SAR 18.6K', tone: 'negative' }],
  confidence: { band: 'High', percent: null, why: ['7.2 months cover here'] },
  drivers: [{ label: 'estimated stock cover', detail: '7.2 months of cover' }],
  evidence: null,
  assumptions: ['Transfer feasibility not modelled in the POC.'],
  lineage: [{ label: 'Inventory workbook', kind: 'workbook' }],
  model: null,
  ownership: null,
  isDemoData: false,
};

const explanationResponse: ExplanationResponse = {
  subjectKind: 'inventory-unit',
  subjectRef: 'STK-1',
  explanation,
  gaps: [],
  feedback: [],
  feedbackCaveat: 'Recorded in this analytics platform only. It does not retrain any model.',
  generatedAtUtc: '2026-07-24T09:00:00Z',
};

const viewer: CurrentUser = {
  isAuthenticated: true,
  subjectId: 'exec-1',
  displayName: 'An Executive',
  roles: ['Executive'],
  permissions: ['recommendation.review', 'explanation-feedback.submit'],
};

const fetchMock = vi.fn<(input: RequestInfo | URL, init?: RequestInit) => Promise<Response>>();

function json(body: unknown) {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'content-type': 'application/json' },
  });
}

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock);
  fetchMock.mockReset();
  fetchMock.mockImplementation((input) => {
    const url = input instanceof Request ? input.url : String(input);
    if (url.includes('/identity/me')) return Promise.resolve(json(viewer));
    if (url.includes('/predictions/explain')) return Promise.resolve(json(explanationResponse));
    return Promise.resolve(json({}));
  });
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('ExplainabilityDrawer (open, populated) has no serious/critical a11y violations', () => {
  for (const theme of THEMES) {
    it(`explainability drawer — ${theme}`, async () => {
      withTheme(theme);
      const client = new QueryClient({
        defaultOptions: { queries: { retry: false, gcTime: 0 } },
      });
      const { container } = render(
        <QueryClientProvider client={client}>
          <ExplainabilityDrawer
            subject={{ kind: 'inventory-unit', ref: 'STK-1' }}
            onClose={() => undefined}
          />
        </QueryClientProvider>,
      );

      // Wait for the payload so the fully-populated panel is what gets audited.
      await screen.findByRole('heading', { name: 'Recommendation' });
      await waitFor(async () => {
        expect(await seriousViolations(container)).toEqual([]);
      });
    });
  }
});
