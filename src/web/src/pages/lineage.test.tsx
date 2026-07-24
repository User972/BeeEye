import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Mock } from 'vitest';
import LineagePage from './lineage';
import type { LineageResponse } from '@/lib/api/lineage';
import { downloadCsv } from '@/lib/csv';

vi.mock('@/lib/csv', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/lib/csv')>();
  return { ...actual, downloadCsv: vi.fn() };
});

function lineage(overrides: Partial<LineageResponse> = {}): LineageResponse {
  return {
    pipeline: [
      { title: 'Oracle Fusion ERP / CRM', description: 'System of record', icon: 'cloud', kind: 'source' },
      { title: 'Secure read-only integration', description: 'Fusion REST / BICC governed extracts · no write-back in this phase', icon: 'vpn_lock', kind: 'integration' },
      { title: 'Curated analytics layer', description: 'Normalised models', icon: 'dataset', kind: 'curated' },
      { title: 'Forecast & decision models', description: 'Demand, order, decay', icon: 'model_training', kind: 'model' },
      { title: 'Explainability service', description: 'Drivers, confidence', icon: 'psychology', kind: 'explain' },
      { title: 'Decision Intelligence application', description: 'This experience', icon: 'insights', kind: 'app' },
    ],
    metrics: [
      { metric: 'Recommended order mix', source: 'Fusion Order Management', basis: 'Sales history + inventory snapshot', state: 'confirmed' },
      { metric: 'Procurement range', source: 'Fusion Procurement', basis: 'Synthetic supplier & PO fixture', state: 'demo' },
      { metric: 'Service-intensity index', source: 'Fusion Service', basis: 'Synthetic service fixture', state: 'demo' },
    ],
    ...overrides,
  };
}

const fetchMock = vi.fn();

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock);
  fetchMock.mockReset();
  (downloadCsv as Mock).mockReset();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

function respondWith(body: LineageResponse) {
  fetchMock.mockResolvedValue(
    new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } }),
  );
}

function respondWithError(status = 500) {
  fetchMock.mockResolvedValue(
    new Response(JSON.stringify({ title: 'Server error', status }), {
      status,
      headers: { 'content-type': 'application/problem+json' },
    }),
  );
}

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  return render(
    <QueryClientProvider client={client}>
      <LineagePage />
    </QueryClientProvider>,
  );
}

describe('Lineage — loading & error (V3-GOV-009)', () => {
  it('shows a loading message while the lineage loads', async () => {
    fetchMock.mockImplementation(() => new Promise(() => {}));
    renderPage();

    expect(await screen.findByText(/loading the lineage/i)).toBeInTheDocument();
  });

  it('shows an actionable error with retry', async () => {
    respondWithError();
    renderPage();

    const buttons = await screen.findAllByRole('button', { name: /retry/i });
    respondWith(lineage());
    fireEvent.click(buttons[0]!);

    expect(await screen.findByText(/oracle fusion erp/i)).toBeInTheDocument();
  });
});

describe('Lineage — populated (V3-GOV-009)', () => {
  it('renders all six pipeline stages in order', async () => {
    respondWith(lineage());
    renderPage();

    await screen.findByText(/1\. Oracle Fusion ERP \/ CRM/);
    expect(screen.getByText(/6\. Decision Intelligence application/)).toBeInTheDocument();
  });

  it('surfaces the no-write-back promise', async () => {
    respondWith(lineage());
    renderPage();

    expect(await screen.findByText(/no write-back in this phase/i)).toBeInTheDocument();
  });

  it('tags each metric confirmed or demo, in words', async () => {
    respondWith(lineage());
    renderPage();

    await screen.findByText('Recommended order mix');
    expect(screen.getByText('Confirmed')).toBeInTheDocument();
    // The two synthetic metrics both render the shared "Demo Data" chip.
    expect(screen.getAllByText('Demo Data').length).toBe(2);
  });
});

describe('Lineage — CSV export (V3-PLAT-007)', () => {
  it('exports the metrics and neutralises a formula-injection cell', async () => {
    respondWith(lineage({ metrics: [{ metric: '@evil', source: 'x', basis: 'y', state: 'confirmed' }] }));
    renderPage();

    // Wait for the populated table (the export button is disabled until data arrives).
    await screen.findByText('@evil');
    fireEvent.click(screen.getByRole('button', { name: /export csv/i }));

    await waitFor(() => expect(downloadCsv as Mock).toHaveBeenCalled());
    const csvText = (downloadCsv as Mock).mock.calls[0]![1] as string;
    expect(csvText).toContain("'@evil");
  });
});
