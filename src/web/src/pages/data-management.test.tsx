import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Mock } from 'vitest';
import DataHealthPage from './data-management';
import type { DataHealthResponse, DataSource } from '@/lib/api/dataHealth';
import { downloadCsv } from '@/lib/csv';

// Keep the real toCsv/escapeCsvField (so injection escaping runs for real); only stub the download.
vi.mock('@/lib/csv', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/lib/csv')>();
  return { ...actual, downloadCsv: vi.fn() };
});

function source(overrides: Partial<DataSource> = {}): DataSource {
  return {
    name: 'Sales history',
    system: 'Fusion Order Management',
    status: 'Ready',
    statusKind: 'ready',
    rows: '3120',
    coverage: 'Jan 2024 → Jun 2026',
    note: 'Supplied workbook — parsed & validated.',
    ...overrides,
  };
}

function health(overrides: Partial<DataHealthResponse> = {}): DataHealthResponse {
  return {
    score: 92,
    scoreBand: 'Healthy',
    salesRows: 3120,
    invRows: 291,
    coverage: 'Jan 2024 → Jun 2026',
    models: 12,
    locations: 5,
    sources: [
      source(),
      source({ name: 'Inventory on-hand', system: 'Fusion Inventory Management', rows: '291' }),
      source({ name: 'Supplier master & PO history', system: 'Fusion Procurement', status: 'Demo data', statusKind: 'demo', rows: 'Synthetic' }),
      source({ name: 'Vehicle mileage & warranty claims', system: 'Fusion Service / CRM', status: 'Blocked', statusKind: 'blocked', rows: '0', coverage: '—' }),
    ],
    issues: [
      { id: 'dup_stock', label: 'Duplicate stock IDs', count: 0, severity: 'ok', note: 'All stock_id values are unique.' },
      { id: 'loc', label: 'Sales locations absent from inventory', count: 2, severity: 'medium', note: 'Abha, Tabuk sell but hold no inventory snapshot.' },
    ],
    generatedAtUtc: '2026-07-24T09:00:00Z',
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

function respondWith(body: DataHealthResponse) {
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
      <DataHealthPage />
    </QueryClientProvider>,
  );
}

describe('Data Health — loading (V3-GOV-008)', () => {
  it('shows placeholders rather than stale numbers while loading', async () => {
    fetchMock.mockImplementation(() => new Promise(() => {}));
    renderPage();

    expect(await screen.findByText(/assessing every data source/i)).toBeInTheDocument();
    expect(screen.getAllByText('—').length).toBeGreaterThan(0);
  });
});

describe('Data Health — populated (V3-GOV-008)', () => {
  it('shows the score with its band as a word, not colour alone', async () => {
    respondWith(health({ score: 92, scoreBand: 'Healthy' }));
    renderPage();

    expect(await screen.findByText('92')).toBeInTheDocument();
    expect(screen.getByText('Healthy')).toBeInTheDocument();
  });

  it('renders every source with a status word', async () => {
    respondWith(health());
    renderPage();

    await screen.findByText('Sales history');
    // Ready / Blocked shown as words; the demo source uses the shared "Demo Data" chip.
    expect(screen.getAllByText('Ready').length).toBeGreaterThan(0);
    expect(screen.getByText('Blocked')).toBeInTheDocument();
    expect(screen.getByText('Demo Data')).toBeInTheDocument();
  });

  it('lists the data-quality checks with severity words', async () => {
    respondWith(health());
    renderPage();

    expect(await screen.findByText('Sales locations absent from inventory')).toBeInTheDocument();
    expect(screen.getByText('Medium')).toBeInTheDocument();
  });

  it('shows the real row counts', async () => {
    respondWith(health());
    renderPage();

    await screen.findByText('Sales history');
    // 3,120 is the formatted sales-rows tile; 291 appears both as a tile and in the sources table.
    expect(screen.getByText('3,120')).toBeInTheDocument();
    expect(screen.getAllByText('291').length).toBeGreaterThan(0);
  });
});

describe('Data Health — empty & error (V3-GOV-008)', () => {
  it('renders an empty state when no source is registered', async () => {
    respondWith(health({ sources: [] }));
    renderPage();

    expect(await screen.findByText('No data sources')).toBeInTheDocument();
  });

  it('shows an actionable error with a retry that refetches', async () => {
    respondWithError();
    renderPage();

    const buttons = await screen.findAllByRole('button', { name: /retry/i });
    respondWith(health());
    fireEvent.click(buttons[0]!);

    expect(await screen.findByText('Sales history')).toBeInTheDocument();
  });
});

describe('Data Health — CSV export (V3-PLAT-007)', () => {
  it('exports the sources and neutralises a formula-injection cell', async () => {
    respondWith(health({ sources: [source({ note: '=SUM(A1:A9)' })] }));
    renderPage();

    // Wait for the populated table (the export button is disabled until data arrives).
    await screen.findByText('Sales history');
    fireEvent.click(screen.getByRole('button', { name: /export csv/i }));

    await waitFor(() => expect(downloadCsv as Mock).toHaveBeenCalled());
    const csvText = (downloadCsv as Mock).mock.calls[0]![1] as string;
    // The leading '=' is neutralised with a single quote so a spreadsheet treats it as text.
    expect(csvText).toContain("'=SUM(A1:A9)");
    expect(csvText).not.toMatch(/(^|,)=SUM/);
  });

  it('disables export while there is nothing to export', async () => {
    respondWith(health({ sources: [] }));
    renderPage();

    await screen.findByText('No data sources');
    const button = screen.getByRole('button', { name: /export csv/i });
    expect(button).toBeDisabled();
  });
});
