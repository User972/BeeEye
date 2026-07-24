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
import ExecutiveCockpit from './executive-cockpit';
import type { Decision, DecisionFeedResponse } from '@/lib/api/executive';
import { navItems } from '@/config/navigation';

/** A decision with realistic defaults; override only what a test cares about. */
function decision(overrides: Partial<Decision> = {}): Decision {
  return {
    id: 'D-INV-1',
    title: 'Redistribute aging inventory — ES 350 ZX',
    area: 'Inventory',
    screen: 'inventory-intelligence',
    severity: 'Medium',
    impactSar: 18_615,
    kind: 'Risk',
    priority: 42,
    confidence: 'Medium',
    confidencePct: 65,
    dueDays: 12,
    whyNow: 'Riyadh holds 3 units the risk model recommends transferring.',
    action: 'Prepare a transfer recommendation: 3 unit(s) Riyadh → Jeddah.',
    evidence: '3 unit(s) · 540,000 SAR stock value',
    ownerRole: 'Inventory Manager',
    isDemo: false,
    factors: [
      { name: 'Business impact', percent: 15 },
      { name: 'Urgency', percent: 60 },
      { name: 'Confidence', percent: 65 },
      { name: 'Controllability', percent: 85 },
    ],
    ...overrides,
  };
}

function feed(overrides: Partial<DecisionFeedResponse> = {}): DecisionFeedResponse {
  const decisions = overrides.decisions ?? [decision()];
  return {
    decisions,
    summary: {
      total: decisions.length,
      critical: 1,
      lowConfidence: 0,
      dueThisWeek: 2,
      opportunityValueSar: 72_095_231,
      riskValueSar: 17_571_054,
      demoDataCount: 0,
    },
    narrative: '5 decisions need attention: 3 relate to inventory, ordering and procurement exposure.',
    gaps: [],
    generatedAtUtc: '2026-06-30T12:00:00Z',
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

function respondWith(body: DecisionFeedResponse) {
  fetchMock.mockResolvedValue(
    new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } }),
  );
}

/**
 * Answers the feed and, separately, the explainability endpoint — the cockpit's two reads once the
 * drawer is wired (V3-UC0x-002).
 */
function respondWithFeedAndExplanation(body: DecisionFeedResponse, explanationTitle: string) {
  fetchMock.mockImplementation((input: string) => {
    const url = String(input);

    if (url.includes('/predictions/explain')) {
      return Promise.resolve(
        new Response(
          JSON.stringify({
            subjectKind: url.includes('kind=brief') ? 'brief' : 'decision',
            subjectRef: 'x',
            explanation: {
              title: explanationTitle,
              module: 'Decision Cockpit',
              label: 'calculated',
              recommendation: null,
              impacts: [],
              confidence: null,
              drivers: [],
              evidence: null,
              assumptions: [],
              lineage: [{ label: 'Sales & inventory workbooks', kind: 'workbook' }],
              model: null,
              ownership: null,
              isDemoData: false,
            },
            gaps: [],
            feedback: [],
            feedbackCaveat: 'Recorded in this analytics platform only.',
            generatedAtUtc: '2026-07-24T09:00:00Z',
          }),
          { status: 200, headers: { 'content-type': 'application/json' } },
        ),
      );
    }

    if (url.includes('/identity/me')) {
      return Promise.resolve(
        new Response(
          JSON.stringify({
            isAuthenticated: true,
            subjectId: 'exec-1',
            displayName: 'An Executive',
            roles: ['Executive'],
            permissions: ['recommendation.review', 'explanation-feedback.submit'],
          }),
          { status: 200, headers: { 'content-type': 'application/json' } },
        ),
      );
    }

    return Promise.resolve(
      new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } }),
    );
  });
}

function respondWithError(status = 500) {
  fetchMock.mockResolvedValue(
    new Response(JSON.stringify({ title: 'Server error', status }), {
      status,
      headers: { 'content-type': 'application/problem+json' },
    }),
  );
}

/** Renders the cockpit inside a real router + query client, so links and fetching behave normally. */
function renderCockpit() {
  const rootRoute = createRootRoute();
  const indexRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/',
    component: ExecutiveCockpit,
  });
  // Every routable screen, so the cockpit's drill-down links resolve.
  const others = navItems
    .filter((i) => i.path !== '/')
    .map((i) =>
      createRoute({
        getParentRoute: () => rootRoute,
        path: i.path,
        component: () => <h1>{i.label}</h1>,
      }),
    );

  const router = createRouter({
    routeTree: rootRoute.addChildren([indexRoute, ...others]),
    history: createMemoryHistory({ initialEntries: ['/'] }),
  });

  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });

  return render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

describe('Decision Cockpit — loading state (V3-UC08-006)', () => {
  it('shows a loading message while the feed is being assessed', async () => {
    fetchMock.mockImplementation(() => new Promise(() => {}));
    renderCockpit();

    expect(await screen.findByText(/assessing every module/i)).toBeInTheDocument();
  });

  it('shows placeholder tiles rather than stale numbers while loading', async () => {
    fetchMock.mockImplementation(() => new Promise(() => {}));
    renderCockpit();

    await screen.findByText(/assessing every module/i);
    expect(screen.getAllByText('—').length).toBeGreaterThan(0);
  });
});

describe('Decision Cockpit — populated state (V3-UC08-005/006)', () => {
  it('replaces the placeholder tiles with real headline figures', async () => {
    respondWith(feed());
    renderCockpit();

    expect(await screen.findByText('Decisions to review')).toBeInTheDocument();
    // No tile is left showing the em-dash placeholder once data has loaded.
    await waitFor(() => expect(screen.queryAllByText('SAR —')).toHaveLength(0));
  });

  it('shows the critical count and the due-this-week hint', async () => {
    respondWith(feed());
    renderCockpit();

    // Wait for real content: the stat-card labels render during loading too, so keying on
    // "Critical" alone would assert against the placeholder state.
    await screen.findByText(/Redistribute aging inventory/);

    expect(screen.getByText('Critical')).toBeInTheDocument();
    expect(screen.getByText('2 due within a week')).toBeInTheDocument();
  });

  it('renders the narrative as the section subtitle', async () => {
    respondWith(feed());
    renderCockpit();

    expect(await screen.findByText(/5 decisions need attention/i)).toBeInTheDocument();
  });

  it('renders one card per decision', async () => {
    respondWith(feed({ decisions: [decision(), decision({ id: 'D-PRT-1', title: 'Increase parts stock' })] }));
    renderCockpit();

    expect(await screen.findByText(/Redistribute aging inventory/)).toBeInTheDocument();
    expect(screen.getByText('Increase parts stock')).toBeInTheDocument();
  });

  it('shows why the decision matters, the recommended action and the evidence', async () => {
    respondWith(feed());
    renderCockpit();

    expect(await screen.findByText(/Riyadh holds 3 units/)).toBeInTheDocument();
    expect(screen.getByText(/Prepare a transfer recommendation/)).toBeInTheDocument();
    expect(screen.getByText(/540,000 SAR stock value/)).toBeInTheDocument();
  });

  it('shows the owner, confidence and review window', async () => {
    respondWith(feed());
    renderCockpit();

    expect(await screen.findByText('Inventory Manager')).toBeInTheDocument();
    expect(screen.getByText('(65%)')).toBeInTheDocument();
    expect(screen.getByText('12 days')).toBeInTheDocument();
  });

  it('renders the four ranked priority drivers', async () => {
    respondWith(feed());
    const { container } = renderCockpit();

    await screen.findByText(/Redistribute aging inventory/);

    // Scoped to the drivers block: "Confidence" is also a metadata label on the same card.
    const drivers = container.querySelector('.decision-card__drivers');
    expect(drivers).not.toBeNull();

    const labels = within(drivers as HTMLElement)
      .getAllByText(/./)
      .map((el) => el.textContent);
    expect(labels).toEqual(
      expect.arrayContaining(['Business impact', 'Urgency', 'Confidence', 'Controllability']),
    );
    expect(within(drivers as HTMLElement).getByText('85%')).toBeInTheDocument();
  });

  it('labels severity in words, not by colour alone', async () => {
    respondWith(feed({ decisions: [decision({ severity: 'High' })] }));
    renderCockpit();

    expect(await screen.findByText('Critical')).toBeInTheDocument();
  });

  it('labels an opportunity differently from an exposure', async () => {
    respondWith(feed({ decisions: [decision({ kind: 'Opportunity' })] }));
    renderCockpit();

    expect(await screen.findByText('Opportunity')).toBeInTheDocument();
    expect(screen.queryByText('Exposure')).not.toBeInTheDocument();
  });

  it('links each decision to the screen that raised it', async () => {
    respondWith(feed());
    renderCockpit();

    const link = await screen.findByRole('link', { name: /Open Inventory Aging & Overstock/ });
    expect(link).toHaveAttribute('href', '/inventory');
  });

  it('navigates to the module screen when the link is followed', async () => {
    respondWith(feed());
    renderCockpit();

    fireEvent.click(await screen.findByRole('link', { name: /Open Inventory Aging & Overstock/ }));

    expect(await screen.findByRole('heading', { name: 'Inventory Aging & Overstock' })).toBeInTheDocument();
  });
});

describe('Decision Cockpit — synthetic data disclosure (V3-DS-002)', () => {
  it('labels decisions derived from synthetic demo data', async () => {
    respondWith(feed({ decisions: [decision({ isDemo: true })] }));
    renderCockpit();

    expect(await screen.findByText('Demo Data')).toBeInTheDocument();
  });

  it('does not label decisions computed from the real dataset', async () => {
    respondWith(feed({ decisions: [decision({ isDemo: false })] }));
    renderCockpit();

    await screen.findByText(/Redistribute aging inventory/);
    expect(screen.queryByText('Demo Data')).not.toBeInTheDocument();
  });
});

describe('Decision Cockpit — empty state (V3-UC08-006)', () => {
  it('says plainly that nothing needs attention', async () => {
    respondWith(feed({ decisions: [], narrative: 'No material exceptions need a decision this period.' }));
    renderCockpit();

    expect(await screen.findByText('No decisions need attention')).toBeInTheDocument();
  });

  it('still shows the headline tiles so zero is distinguishable from unknown', async () => {
    respondWith(feed({ decisions: [] }));
    renderCockpit();

    await screen.findByText('No decisions need attention');
    expect(screen.getByText('Decisions to review')).toBeInTheDocument();
  });
});

describe('Decision Cockpit — error state (V3-UC08-006)', () => {
  it('shows an actionable error rather than a blank screen', async () => {
    respondWithError();
    renderCockpit();

    expect(await screen.findByText(/could not load the decision feed/i)).toBeInTheDocument();
  });

  it('offers a retry that refetches', async () => {
    respondWithError();
    renderCockpit();

    const retry = await screen.findByRole('button', { name: /retry/i });
    respondWith(feed());
    fireEvent.click(retry);

    expect(await screen.findByText(/Redistribute aging inventory/)).toBeInTheDocument();
  });

  it('reports a network failure without exposing internals', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'));
    renderCockpit();

    expect(await screen.findByText(/could not load the decision feed/i)).toBeInTheDocument();
  });
});

describe('Decision Cockpit — partial-failure state (V3-UC08-004)', () => {
  it('warns when a module could not be assessed, so a gap is never mistaken for calm', async () => {
    respondWith(
      feed({
        gaps: [{ area: 'Parts', reason: 'This area could not be assessed. Its decisions are missing.' }],
      }),
    );
    renderCockpit();

    // The loading indicator also carries role="status", so wait for real content first.
    await screen.findByText(/Redistribute aging inventory/);

    const status = screen.getByRole('status');
    expect(within(status).getByText(/this view is incomplete/i)).toBeInTheDocument();
    expect(within(status).getByText(/Parts/)).toBeInTheDocument();
  });

  it('shows no warning when every module reported', async () => {
    respondWith(feed());
    renderCockpit();

    await screen.findByText(/Redistribute aging inventory/);
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });
});

describe('Decision Cockpit — explainability (V3-UC0x-002)', () => {
  it('explains a single decision card, and the brief itself', async () => {
    respondWithFeedAndExplanation(feed(), 'How this monthly brief was generated');
    renderCockpit();

    // v3's ckExplainSummary — the brief is itself a thing that needs explaining.
    fireEvent.click(
      await screen.findByRole('button', {
        name: /why this brief\? how this monthly brief was generated/i,
      }),
    );

    expect(await screen.findByRole('dialog')).toBeInTheDocument();
    expect(await screen.findByText('How this monthly brief was generated')).toBeInTheDocument();

    // And each ranked decision carries its own trigger, named for that decision.
    expect(
      screen.getByRole('button', {
        name: /why this recommendation\? redistribute aging inventory — es 350 zx/i,
      }),
    ).toBeInTheDocument();
  });
});
