import type { ReactNode } from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { axe } from 'vitest-axe';
import type { Result } from 'axe-core';
import DataHealthPage from './data-management';
import LineagePage from './lineage';
import SettingsPage from './platform-settings';
import type { DataHealthResponse } from '@/lib/api/dataHealth';
import type { LineageResponse } from '@/lib/api/lineage';
import type { SettingsResponse } from '@/lib/api/settings';

// ---------------------------------------------------------------------------
// Component-level accessibility for the S7 governance screens (V3-QA-003).
// jsdom, so this catches structural/ARIA defects; colour-contrast is audited at the route level in a
// real browser (e2e/a11y.spec.ts). Both themes are toggled. The bar is zero serious/critical.
// ---------------------------------------------------------------------------

const THEMES = ['light', 'dark'] as const;

async function seriousViolations(container: HTMLElement): Promise<Result[]> {
  const results = await axe(container, { rules: { 'color-contrast': { enabled: false } } });
  return results.violations.filter((v) => v.impact === 'serious' || v.impact === 'critical');
}

function withTheme(theme: (typeof THEMES)[number]): void {
  document.documentElement.dataset['theme'] = theme;
}

const dataHealth: DataHealthResponse = {
  score: 92,
  scoreBand: 'Healthy',
  salesRows: 3120,
  invRows: 291,
  coverage: 'Jan 2024 → Jun 2026',
  models: 12,
  locations: 5,
  sources: [
    { name: 'Sales history', system: 'Fusion Order Management', status: 'Ready', statusKind: 'ready', rows: '3120', coverage: 'Jan 2024 → Jun 2026', note: 'Supplied workbook.' },
    { name: 'Supplier master & PO history', system: 'Fusion Procurement', status: 'Demo data', statusKind: 'demo', rows: 'Synthetic', coverage: 'Trailing 18 months (synthetic)', note: 'Not supplied.' },
    { name: 'Vehicle mileage & warranty claims', system: 'Fusion Service / CRM', status: 'Blocked', statusKind: 'blocked', rows: '0', coverage: '—', note: 'Not available in sample.' },
  ],
  issues: [{ id: 'loc', label: 'Sales locations absent from inventory', count: 0, severity: 'ok', note: 'None.' }],
  generatedAtUtc: '2026-07-24T09:00:00Z',
};

const lineage: LineageResponse = {
  pipeline: [
    { title: 'Oracle Fusion ERP / CRM', description: 'System of record', icon: 'cloud', kind: 'source' },
    { title: 'Secure read-only integration', description: 'No write-back in this phase', icon: 'vpn_lock', kind: 'integration' },
  ],
  metrics: [
    { metric: 'Recommended order mix', source: 'Fusion Order Management', basis: 'Sales history', state: 'confirmed' },
    { metric: 'Procurement range', source: 'Fusion Procurement', basis: 'Synthetic supplier & PO fixture', state: 'demo' },
  ],
};

const settings: SettingsResponse = {
  weights: { cover: 30, aging: 25, demand: 20, holding: 15, lead: 10, sum: 100, note: 'Renormalised by their sum.' },
  riskBands: [
    { label: 'Low', threshold: 34, range: '0–34' },
    { label: 'Critical', threshold: null, range: '80+' },
  ],
  agingBands: [
    { label: 'New', threshold: 30, range: '0–30' },
    { label: 'Critical aging', threshold: null, range: '121+' },
  ],
  analysisDate: '30 Jun 2026',
  trailingMonths: 3,
  coverMax: 6,
  note: 'Current configuration, read-only.',
};

const fetchMock = vi.fn();

function json(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } });
}

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock);
  fetchMock.mockReset();
});

afterEach(() => {
  vi.unstubAllGlobals();
  delete document.documentElement.dataset['theme'];
});

function renderPage(node: ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  return render(<QueryClientProvider client={client}>{node}</QueryClientProvider>);
}

describe('S7 governance screens have no serious/critical a11y violations (both themes)', () => {
  for (const theme of THEMES) {
    it(`Data Health — ${theme}`, async () => {
      withTheme(theme);
      fetchMock.mockResolvedValue(json(dataHealth));
      const { container } = renderPage(<DataHealthPage />);

      await screen.findByText('Sales history');
      await waitFor(async () => expect(await seriousViolations(container)).toEqual([]));
    });

    it(`Lineage — ${theme}`, async () => {
      withTheme(theme);
      fetchMock.mockResolvedValue(json(lineage));
      const { container } = renderPage(<LineagePage />);

      await screen.findByText('Recommended order mix');
      await waitFor(async () => expect(await seriousViolations(container)).toEqual([]));
    });

    it(`Settings — ${theme}`, async () => {
      withTheme(theme);
      fetchMock.mockResolvedValue(json(settings));
      const { container } = renderPage(<SettingsPage />);

      await screen.findByText('Risk-factor weights');
      await waitFor(async () => expect(await seriousViolations(container)).toEqual([]));
    });
  }
});
