import { fireEvent, render, screen, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import SettingsPage from './platform-settings';
import type { SettingsResponse } from '@/lib/api/settings';

function settings(overrides: Partial<SettingsResponse> = {}): SettingsResponse {
  return {
    weights: {
      cover: 30,
      aging: 25,
      demand: 20,
      holding: 15,
      lead: 10,
      sum: 100,
      note: 'The engine renormalises the weights by their sum (100), so they need not total 100.',
    },
    riskBands: [
      { label: 'Low', threshold: 34, range: '0–34' },
      { label: 'Medium', threshold: 59, range: '35–59' },
      { label: 'High', threshold: 79, range: '60–79' },
      { label: 'Critical', threshold: null, range: '80+' },
    ],
    agingBands: [
      { label: 'New', threshold: 30, range: '0–30' },
      { label: 'Healthy', threshold: 60, range: '31–60' },
      { label: 'Watch', threshold: 90, range: '61–90' },
      { label: 'High attention', threshold: 120, range: '91–120' },
      { label: 'Critical aging', threshold: null, range: '121+' },
    ],
    analysisDate: '30 Jun 2026',
    trailingMonths: 3,
    coverMax: 6,
    note: 'These are the platform\'s current configuration values, shown read-only.',
    ...overrides,
  };
}

const fetchMock = vi.fn();

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock);
  fetchMock.mockReset();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

function respondWith(body: SettingsResponse) {
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
      <SettingsPage />
    </QueryClientProvider>,
  );
}

describe('Settings — loading & error (V3-GOV-010)', () => {
  it('shows a loading message', async () => {
    fetchMock.mockImplementation(() => new Promise(() => {}));
    renderPage();

    expect(await screen.findByText(/loading the configuration/i)).toBeInTheDocument();
  });

  it('shows an actionable error with retry', async () => {
    respondWithError();
    renderPage();

    const retry = await screen.findByRole('button', { name: /retry/i });
    respondWith(settings());
    fireEvent.click(retry);

    expect(await screen.findByText('Risk-factor weights')).toBeInTheDocument();
  });
});

describe('Settings — populated, read-only (V3-GOV-010)', () => {
  it('badges the screen as current configuration, read-only', async () => {
    respondWith(settings());
    renderPage();

    expect((await screen.findAllByText(/current configuration · read-only/i)).length).toBeGreaterThan(0);
  });

  it('shows the risk-factor weights and their sum', async () => {
    respondWith(settings());
    renderPage();

    const weightsCard = (await screen.findByText('Risk-factor weights')).closest('.card') as HTMLElement;
    const table = within(weightsCard);
    expect(table.getByText('Stock cover')).toBeInTheDocument();
    expect(table.getByText('30')).toBeInTheDocument();
    expect(table.getByText('25')).toBeInTheDocument();
    expect(table.getByText('Sum')).toBeInTheDocument();
    expect(table.getByText('100')).toBeInTheDocument();
  });

  it('renders the risk bands with thresholds and labels', async () => {
    respondWith(settings());
    renderPage();

    await screen.findByText('Risk bands');
    expect(screen.getByText('Low')).toBeInTheDocument();
    expect(screen.getByText('0–34')).toBeInTheDocument();
    expect(screen.getByText('Critical')).toBeInTheDocument();
    expect(screen.getByText('80+')).toBeInTheDocument();
  });

  it('renders the aging bands with the five labels', async () => {
    respondWith(settings());
    renderPage();

    await screen.findByText('Aging bands');
    for (const label of ['New', 'Healthy', 'Watch', 'High attention', 'Critical aging']) {
      expect(screen.getByText(label)).toBeInTheDocument();
    }
    expect(screen.getByText('121+')).toBeInTheDocument();
  });

  it('shows the analysis date, horizon and cover ceiling', async () => {
    respondWith(settings());
    renderPage();

    expect(await screen.findByText('30 Jun 2026')).toBeInTheDocument();
    expect(screen.getByText('Trailing months')).toBeInTheDocument();
    expect(screen.getByText('Cover ceiling')).toBeInTheDocument();
  });

  it('never invents a cover-target value', async () => {
    respondWith(settings());
    renderPage();

    await screen.findByText('Risk-factor weights');
    expect(screen.queryByText(/cover target/i)).not.toBeInTheDocument();
  });
});
