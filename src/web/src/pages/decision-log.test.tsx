import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import {
  RouterProvider,
  createMemoryHistory,
  createRootRoute,
  createRoute,
  createRouter,
} from '@tanstack/react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import DecisionLog from './decision-log';
import type { DecisionDetail, DecisionLogItem, DecisionLogPage } from '@/lib/api/decisions';
import type { CurrentUser } from '@/lib/api/identity';

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

function item(overrides: Partial<DecisionLogItem> = {}): DecisionLogItem {
  return {
    recommendationId: '11111111-1111-1111-1111-111111111111',
    decisionId: null,
    ruleId: 'D-INV-1',
    subjectRef: 'ES 350 ZX · Pearl White',
    area: 'Inventory',
    action: 'Transfer 40 unit(s) Riyadh → Jeddah.',
    evidence: '3 unit(s) · 540,000 SAR stock value',
    status: 'Generated',
    outcome: null,
    impactSar: 18_615,
    priority: 42,
    ownerRole: 'Inventory Manager',
    isDemoData: false,
    source: 'Rule D-INV-1 · ruleset v1',
    createdAtUtc: '2026-06-30T09:00:00Z',
    decidedBy: null,
    decidedAtUtc: null,
    modification: null,
    availableActions: ['claim'],
    ...overrides,
  };
}

function page(overrides: Partial<DecisionLogPage> = {}): DecisionLogPage {
  const items = overrides.items ?? [item()];
  const counts: Record<string, number> = {
    Generated: 0,
    UnderReview: 0,
    Accepted: 0,
    AcceptedModified: 0,
    Rejected: 0,
    Expired: 0,
    Superseded: 0,
    Implemented: 0,
    OutcomeRecorded: 0,
  };
  for (const i of items) counts[i.status] = (counts[i.status] ?? 0) + 1;

  return {
    items,
    page: 1,
    pageSize: 50,
    totalCount: items.length,
    statusCounts: counts,
    ...overrides,
  };
}

function detail(overrides: Partial<DecisionDetail> = {}): DecisionDetail {
  return {
    recommendation: {
      id: '11111111-1111-1111-1111-111111111111',
      ruleId: 'D-INV-1',
      subjectRef: 'ES 350 ZX · Pearl White',
      area: 'Inventory',
      action: 'Transfer 40 unit(s) Riyadh → Jeddah.',
      rationale: 'Riyadh holds units the risk model recommends moving.',
      evidence: ['3 unit(s) · 540,000 SAR stock value'],
      expectedOutcome: 'Historically moved more volume in the receiving region.',
      confidence: 'Medium',
      assumptions: ['Inventory metrics reflect the analysis-date assumption.'],
      impactSar: 18_615,
      priority: 42,
      ownerRole: 'Inventory Manager',
      isDemoData: false,
      rulesetVersion: 'v1',
      datasetVersion: 'workbook-2026-06',
      analysisDate: '2026-06-30',
      currentStatus: 'AcceptedModified',
      validUntilUtc: '2026-07-30T09:00:00Z',
      supersededByRecommendationId: null,
      createdAtUtc: '2026-06-30T09:00:00Z',
    },
    decision: {
      id: '22222222-2222-2222-2222-222222222222',
      outcome: 'AcceptedModified',
      openedBy: 'analyst-1',
      openedAtUtc: '2026-06-30T10:00:00Z',
      decidedBy: 'exec-1',
      decidedAtUtc: '2026-06-30T11:00:00Z',
      note: 'Trimmed for showroom capacity',
      modification: { field: 'proposed_qty', from: 40, to: 30, rationale: 'Capacity' },
      implementedBy: null,
      implementedAtUtc: null,
    },
    approvalSteps: [
      {
        stepNumber: 1,
        approverRole: 'Inventory Manager',
        status: 'Pending',
        actedBy: null,
        actedAtUtc: null,
        note: null,
      },
    ],
    statusEvents: [
      { fromStatus: null, toStatus: 'Generated', actor: 'system', reason: null, atUtc: '2026-06-30T09:00:00Z' },
      {
        fromStatus: 'Generated',
        toStatus: 'UnderReview',
        actor: 'analyst-1',
        reason: 'Claimed for review.',
        atUtc: '2026-06-30T10:00:00Z',
      },
    ],
    outcome: null,
    availableActions: [],
    ...overrides,
  };
}

const approver: CurrentUser = {
  isAuthenticated: true,
  subjectId: 'exec-1',
  displayName: 'An Executive',
  roles: ['Executive'],
  permissions: ['recommendation.review', 'recommendation.approve', 'decision-outcome.record'],
};

const reviewerOnly: CurrentUser = {
  isAuthenticated: true,
  subjectId: 'analyst-1',
  displayName: 'An Analyst',
  roles: ['Analyst'],
  permissions: ['recommendation.review', 'recommendation.generate'],
};

// ---------------------------------------------------------------------------
// Fetch harness — routes by URL so identity, log and detail can answer differently.
// ---------------------------------------------------------------------------

const fetchMock = vi.fn();

interface Routes {
  identity?: CurrentUser;
  log?: DecisionLogPage;
  detail?: DecisionDetail;
  logStatus?: number;
  logProblem?: { title?: string; detail?: string };
  onPost?: (url: string, init: RequestInit) => Response | Promise<Response>;
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function problem(status: number, body: { title?: string; detail?: string }) {
  return new Response(JSON.stringify({ status, ...body }), {
    status,
    headers: { 'content-type': 'application/problem+json' },
  });
}

function serve(routes: Routes) {
  fetchMock.mockImplementation((input: string, init?: RequestInit) => {
    const url = String(input);

    if (init?.method === 'POST') {
      if (routes.onPost) return Promise.resolve(routes.onPost(url, init));
      return Promise.resolve(
        json({
          recommendationId: '11111111-1111-1111-1111-111111111111',
          decisionId: '22222222-2222-2222-2222-222222222222',
          status: 'UnderReview',
          outcome: 'Open',
          message: 'You now own this recommendation.',
        }),
      );
    }

    if (url.includes('/identity/me')) {
      return Promise.resolve(json(routes.identity ?? approver));
    }

    if (/\/decisions\/[0-9a-f-]{36}/.test(url)) {
      return Promise.resolve(json(routes.detail ?? detail()));
    }

    if (routes.logStatus && routes.logStatus >= 400) {
      return Promise.resolve(problem(routes.logStatus, routes.logProblem ?? {}));
    }

    return Promise.resolve(json(routes.log ?? page()));
  });
}

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock);
  fetchMock.mockReset();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

function renderLog() {
  const rootRoute = createRootRoute();
  const indexRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/decisions',
    component: DecisionLog,
  });
  const home = createRoute({
    getParentRoute: () => rootRoute,
    path: '/',
    component: () => <h1>Home</h1>,
  });

  const router = createRouter({
    routeTree: rootRoute.addChildren([indexRoute, home]),
    history: createMemoryHistory({ initialEntries: ['/decisions'] }),
  });

  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } },
  });

  return render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

// ---------------------------------------------------------------------------

describe('Decision Log — rows and chips (V3-GOV-001)', () => {
  it('renders a row per recommendation with its id, subject and area', async () => {
    serve({ log: page() });
    renderLog();

    expect(await screen.findByText('ES 350 ZX · Pearl White')).toBeInTheDocument();
    expect(screen.getByText('D-INV-1')).toBeInTheDocument();
    expect(screen.getByText('Inventory')).toBeInTheDocument();
  });

  it('shows the evidence line and the source · created · impact footer', async () => {
    serve({ log: page() });
    renderLog();

    expect(await screen.findByText(/540,000 SAR stock value/)).toBeInTheDocument();
    expect(screen.getByText(/Rule D-INV-1 · ruleset v1/)).toBeInTheDocument();
    expect(screen.getByText(/expected impact/)).toBeInTheDocument();
  });

  it('renders a status chip per non-empty status, with its count', async () => {
    serve({
      log: page({
        items: [item(), item({ recommendationId: 'b', status: 'Rejected', availableActions: [] })],
      }),
    });
    renderLog();

    const chips = await screen.findByRole('group', { name: /filter by status/i });

    expect(within(chips).getByRole('button', { name: /New/ })).toBeInTheDocument();
    expect(within(chips).getByRole('button', { name: /Rejected/ })).toBeInTheDocument();
    // A status nobody is in is hidden rather than shown as a zero.
    expect(within(chips).queryByRole('button', { name: /Superseded/ })).not.toBeInTheDocument();
  });

  it('states the status in words, never by colour alone', async () => {
    serve({ log: page({ items: [item({ status: 'UnderReview' })] }) });
    renderLog();

    // Once in the chip row, once on the row itself.
    expect(await screen.findAllByText('Under review')).not.toHaveLength(0);
  });

  it('labels a record derived from synthetic demo data', async () => {
    serve({ log: page({ items: [item({ isDemoData: true })] }) });
    renderLog();

    expect(await screen.findByText('Demo data')).toBeInTheDocument();
  });

  it('has no delete control anywhere on the screen', async () => {
    serve({ log: page() });
    renderLog();

    await screen.findByText('ES 350 ZX · Pearl White');

    expect(screen.queryByRole('button', { name: /delete/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /remove/i })).not.toBeInTheDocument();
  });
});

describe('Decision Log — filtering', () => {
  it('filters when a chip is pressed and marks it pressed', async () => {
    serve({ log: page() });
    renderLog();

    const chip = await screen.findByRole('button', { name: /New/ });
    expect(chip).toHaveAttribute('aria-pressed', 'false');

    fireEvent.click(chip);

    await waitFor(() => expect(chip).toHaveAttribute('aria-pressed', 'true'));
    await waitFor(() =>
      expect(fetchMock.mock.calls.some(([url]) => String(url).includes('status=Generated'))).toBe(true),
    );
  });

  it('offers a clear-filter control once a chip is active', async () => {
    serve({ log: page() });
    renderLog();

    fireEvent.click(await screen.findByRole('button', { name: /New/ }));

    expect(await screen.findByRole('button', { name: /clear filter/i })).toBeInTheDocument();
  });
});

describe('Decision Log — states', () => {
  it('shows a loading state while the log is being fetched', async () => {
    fetchMock.mockImplementation(() => new Promise(() => {}));
    renderLog();

    expect(await screen.findByText(/loading the decision log/i)).toBeInTheDocument();
  });

  it('shows the empty state with the Oracle Fusion caveat when nothing is logged', async () => {
    serve({ log: page({ items: [] }) });
    renderLog();

    expect(await screen.findByText('No decisions logged yet')).toBeInTheDocument();
    expect(screen.getByText(/not written back to Oracle Fusion/i)).toBeInTheDocument();
  });

  it('distinguishes no-match from empty, and offers a way out of the filter', async () => {
    // Rows are empty but the counts say records exist — the user has filtered themselves into a
    // corner, which is a different problem from having no records at all.
    serve({
      log: page({
        items: [],
        statusCounts: {
          Generated: 0,
          UnderReview: 0,
          Accepted: 4,
          AcceptedModified: 0,
          Rejected: 0,
          Expired: 0,
          Superseded: 0,
          Implemented: 0,
          OutcomeRecorded: 0,
        },
      }),
    });
    renderLog();

    expect(await screen.findByText(/no decisions match this status filter/i)).toBeInTheDocument();
    expect(screen.queryByText('No decisions logged yet')).not.toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /clear filter/i }).length).toBeGreaterThan(0);
  });

  it('shows an actionable error rather than a blank screen', async () => {
    serve({ logStatus: 500, logProblem: { title: 'Server error', detail: 'Something broke.' } });
    renderLog();

    expect(await screen.findByText(/could not load the decision log/i)).toBeInTheDocument();
    expect(screen.getByText('Something broke.')).toBeInTheDocument();
  });

  it('reports a network failure without exposing internals', async () => {
    fetchMock.mockImplementation((input: string) =>
      String(input).includes('/identity/me')
        ? Promise.resolve(json(approver))
        : Promise.reject(new TypeError('Failed to fetch')),
    );
    renderLog();

    expect(await screen.findByText(/could not load the decision log/i)).toBeInTheDocument();
    expect(screen.getByText(/did not respond/i)).toBeInTheDocument();
  });

  it('explains a permission refusal instead of rendering an empty table', async () => {
    serve({ logStatus: 403 });
    renderLog();

    expect(await screen.findByText(/do not have access to the decision log/i)).toBeInTheDocument();
    expect(screen.getByText(/recommendation\.review/)).toBeInTheDocument();
  });
});

describe('Decision Log — transitions offered', () => {
  it('offers only the actions the server reported for the row', async () => {
    serve({ log: page({ items: [item({ status: 'Generated', availableActions: ['claim'] })] }) });
    renderLog();

    expect(await screen.findByRole('button', { name: /claim for review/i })).toBeInTheDocument();

    // Absent, not present-and-disabled: a control that never enables is a support ticket.
    expect(screen.queryByRole('button', { name: /^accept$/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /reject/i })).not.toBeInTheDocument();
  });

  it('offers every verdict on a claimed row for an approver', async () => {
    serve({
      log: page({
        items: [
          item({
            status: 'UnderReview',
            decisionId: '22222222-2222-2222-2222-222222222222',
            outcome: 'Open',
            availableActions: ['accept', 'accept-with-modification', 'reject', 'sign-off'],
          }),
        ],
      }),
    });
    renderLog();

    expect(await screen.findByRole('button', { name: /^accept$/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /accept with modification/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^reject$/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /claim for review/i })).not.toBeInTheDocument();
  });

  it('explains in one line why a reviewer sees no verdict controls', async () => {
    serve({
      identity: reviewerOnly,
      log: page({
        items: [
          item({ status: 'UnderReview', decisionId: '2', outcome: 'Open', availableActions: [] }),
        ],
      }),
    });
    renderLog();

    expect(await screen.findByText(/requires approver permission/i)).toBeInTheDocument();
  });
});

describe('Decision Log — rejecting', () => {
  const rejectable = page({
    items: [
      item({
        status: 'UnderReview',
        decisionId: '22222222-2222-2222-2222-222222222222',
        outcome: 'Open',
        availableActions: ['reject'],
      }),
    ],
  });

  it('blocks a rejection with no reason client-side', async () => {
    serve({ log: rejectable });
    renderLog();

    // Two controls share the word: the row action that opens the form, and the form's submit.
    fireEvent.click(await screen.findByRole('button', { name: /^reject$/i }));
    const form = await screen.findByLabelText(/reason \(required\)/i);
    fireEvent.submit(form.closest('form')!);

    expect(await screen.findByRole('alert')).toHaveTextContent(/reason is required/i);
    expect(fetchMock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'POST')).toBe(
      false,
    );
  });

  it("renders the server's 400 when the client check is bypassed", async () => {
    serve({
      log: rejectable,
      onPost: () =>
        problem(400, {
          title: 'Invalid request',
          detail: 'A reason is required to reject a recommendation.',
        }),
    });
    renderLog();

    fireEvent.click(await screen.findByRole('button', { name: /^reject$/i }));
    const note = await screen.findByLabelText(/reason \(required\)/i);
    fireEvent.change(note, { target: { value: 'no' } });
    fireEvent.submit(note.closest('form')!);

    expect(await screen.findByText(/A reason is required to reject a recommendation\./)).toBeInTheDocument();
  });
});

describe('Decision Log — accepting with a modification', () => {
  const modifiable = page({
    items: [
      item({
        status: 'UnderReview',
        decisionId: '22222222-2222-2222-2222-222222222222',
        outcome: 'Open',
        availableActions: ['accept-with-modification'],
      }),
    ],
  });

  it('surfaces the server refusal for a discount outside the observed band', async () => {
    serve({
      log: modifiable,
      onPost: () =>
        problem(422, {
          title: 'That change cannot be accepted',
          detail: 'A discount must stay within the historically observed 0–20% range.',
        }),
    });
    renderLog();

    fireEvent.click(await screen.findByRole('button', { name: /accept with modification/i }));
    fireEvent.change(screen.getByLabelText(/value to change/i), { target: { value: 'discount_pct' } });
    fireEvent.change(screen.getByLabelText(/recommended/i), { target: { value: '15' } });
    fireEvent.change(screen.getByLabelText(/changed to/i), { target: { value: '25' } });
    fireEvent.click(screen.getAllByRole('button', { name: /accept with modification/i })[1]!);

    expect(await screen.findByText(/0–20% range/)).toBeInTheDocument();
  });

  it('shows the stored delta on a row that was modified', async () => {
    serve({
      log: page({
        items: [
          item({
            status: 'AcceptedModified',
            outcome: 'AcceptedModified',
            decidedBy: 'exec-1',
            modification: { field: 'proposed_qty', from: 40, to: 30, rationale: null },
            availableActions: [],
          }),
        ],
      }),
    });
    renderLog();

    expect(await screen.findByText(/proposed_qty: 40 → 30/)).toBeInTheDocument();
    expect(screen.getByText(/Decided by exec-1/)).toBeInTheDocument();
  });
});

describe('Decision Log — conflicts', () => {
  it("surfaces the server's explanation and refetches when someone else moved the record", async () => {
    serve({
      log: page({
        items: [
          item({
            status: 'UnderReview',
            decisionId: '22222222-2222-2222-2222-222222222222',
            outcome: 'Open',
            availableActions: ['accept'],
          }),
        ],
      }),
      onPost: () =>
        problem(409, {
          title: 'That is not possible right now',
          detail: 'Someone else updated this recommendation while you were working on it.',
        }),
    });
    renderLog();

    fireEvent.click(await screen.findByRole('button', { name: /^accept$/i }));

    expect(await screen.findByText(/Someone else updated this recommendation/)).toBeInTheDocument();

    // The log is refetched so the user sees the state it is actually in, not the one they assumed.
    await waitFor(() => {
      const gets = fetchMock.mock.calls.filter(
        ([url, init]) =>
          String(url).includes('/api/v1/decisions?') && (init as RequestInit | undefined)?.method !== 'POST',
      );
      expect(gets.length).toBeGreaterThan(1);
    });
  });
});

describe('Decision Log — claiming', () => {
  it('sends an Idempotency-Key with every write', async () => {
    serve({ log: page() });
    renderLog();

    fireEvent.click(await screen.findByRole('button', { name: /claim for review/i }));

    await waitFor(() => {
      const post = fetchMock.mock.calls.find(
        ([, init]) => (init as RequestInit | undefined)?.method === 'POST',
      );
      expect(post).toBeDefined();
      const headers = (post?.[1] as RequestInit).headers as Record<string, string>;
      expect(headers['Idempotency-Key']).toMatch(/.{8,}/);
    });
  });

  it('reports a bulk claim honestly rather than with a blanket success', async () => {
    let call = 0;
    serve({
      log: page({
        items: [
          item({ recommendationId: 'a' }),
          item({ recommendationId: 'b' }),
        ],
      }),
      onPost: () => {
        call += 1;
        return call === 1
          ? json({
              recommendationId: 'a',
              decisionId: 'd',
              status: 'UnderReview',
              outcome: 'Open',
              message: 'ok',
            })
          : problem(409, { detail: 'Someone else is already reviewing this recommendation.' });
      },
    });
    renderLog();

    fireEvent.click(await screen.findByRole('button', { name: /log current cockpit decisions/i }));

    expect(await screen.findByText(/1 of 2 claimed/)).toBeInTheDocument();
    expect(screen.getByText(/already claimed by someone else/)).toBeInTheDocument();
  });
});

describe('Decision Log — detail drawer', () => {
  it('shows the frozen original beside the human decision', async () => {
    serve({ log: page(), detail: detail() });
    renderLog();

    fireEvent.click(await screen.findByRole('button', { name: /open evidence/i }));

    const dialog = await screen.findByRole('dialog');

    // Awaited: the drawer opens on its loading state while the detail query is in flight.
    expect(await within(dialog).findByText('What the system recommended')).toBeInTheDocument();
    expect(within(dialog).getByText('What the human decided')).toBeInTheDocument();
    expect(within(dialog).getByText(/Riyadh holds units the risk model/)).toBeInTheDocument();
    expect(within(dialog).getByText(/Ruleset v1 · dataset workbook-2026-06/)).toBeInTheDocument();
    expect(within(dialog).getByText(/proposed_qty: 40 → 30/)).toBeInTheDocument();
  });

  it('shows the full status-event timeline', async () => {
    serve({ log: page(), detail: detail() });
    renderLog();

    fireEvent.click(await screen.findByRole('button', { name: /open evidence/i }));
    const dialog = await screen.findByRole('dialog');

    expect(await within(dialog).findByText(/Claimed for review\./)).toBeInTheDocument();
    expect(within(dialog).getByText('New')).toBeInTheDocument();
  });

  it('returns focus to the control that opened it', async () => {
    serve({ log: page(), detail: detail() });
    renderLog();

    const opener = await screen.findByRole('button', { name: /open evidence/i });
    opener.focus();
    fireEvent.click(opener);

    const dialog = await screen.findByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: /close panel/i }));

    await waitFor(() => expect(document.activeElement).toBe(opener));
  });

  it('says plainly when nobody has claimed the record yet', async () => {
    serve({ log: page(), detail: detail({ decision: null, approvalSteps: [] }) });
    renderLog();

    fireEvent.click(await screen.findByRole('button', { name: /open evidence/i }));

    expect(await screen.findByText(/Nobody has claimed this recommendation yet/)).toBeInTheDocument();
  });
});

describe('Decision Log — governance banner', () => {
  it('keeps the caveat that reaching a later state is not an Oracle transaction', async () => {
    serve({ log: page() });
    renderLog();

    expect(
      await screen.findByText(/not confirmation that Oracle\s+transactions were executed/i),
    ).toBeInTheDocument();
  });
});
