import { useState } from 'react';
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
import { ExplainabilityDrawer, ExplainButton } from './ExplainabilityDrawer';
import { useExplainabilityDrawer } from './useExplainabilityDrawer';
import { Drawer } from '@/components/ui/Drawer';
import type { Explanation, ExplanationResponse } from '@/lib/api/explainability';
import type { CurrentUser } from '@/lib/api/identity';

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

function explanation(overrides: Partial<Explanation> = {}): Explanation {
  return {
    title: 'ES 350 ZX · STK-1',
    module: 'Inventory Intelligence',
    label: 'recommendation',
    recommendation: 'Transfer stock. Riyadh holds elevated cover while Jeddah shows stronger demand.',
    impacts: [
      { label: 'Holding cost accrued', value: 'SAR 18.6K', tone: 'negative' },
      { label: 'Stock value', value: 'SAR 540.0K', tone: 'neutral' },
    ],
    confidence: {
      band: 'High',
      percent: null,
      why: ['7.2 months cover here', 'Jeddah: 1.1 months cover'],
    },
    drivers: [
      { label: 'estimated stock cover', detail: '7.2 months of cover · 18.0 of 71 risk points' },
      { label: 'inventory holding age', detail: '184 days in stock · 14.2 of 71 risk points' },
    ],
    evidence: {
      period: 'Contribution to the 0–100 risk score',
      points: [
        { label: 'estimated stock cover', value: 18, comparison: null },
        { label: 'inventory holding age', value: 14.2, comparison: null },
      ],
      note: 'Weighted contributions, renormalised to a 0–100 score.',
      valueLabel: 'Risk points',
      comparisonLabel: null,
    },
    assumptions: ['Transfer feasibility and logistics cost not modelled in the POC.'],
    lineage: [
      { label: 'Inventory workbook (inventory.json)', kind: 'workbook' },
      { label: 'Overstock risk model (UC5)', kind: 'derived' },
    ],
    model: {
      name: 'Overstock & aging risk model',
      version: 'UC5 · additive weighted model',
      recalculated: '30 Jun 2026',
      horizon: '3-month trailing demand window',
      validation: 'Deterministic rule set — reproducible from the same inputs',
      error: 'rule-based',
    },
    ownership: {
      ownerRole: 'Inventory Manager',
      status: 'High risk · Critical aging',
      decisionSubjectRef: 'ES 350 ZX',
    },
    isDemoData: false,
    ...overrides,
  };
}

const CAVEAT =
  'Recorded in this analytics platform only. It does not retrain any model and does not change any recommendation.';

function response(overrides: Partial<ExplanationResponse> = {}): ExplanationResponse {
  return {
    subjectKind: 'inventory-unit',
    subjectRef: 'STK-1',
    explanation: explanation(),
    gaps: [],
    feedback: [],
    feedbackCaveat: CAVEAT,
    generatedAtUtc: '2026-07-24T09:00:00Z',
    ...overrides,
  };
}

const viewer: CurrentUser = {
  isAuthenticated: true,
  subjectId: 'exec-1',
  displayName: 'An Executive',
  roles: ['Executive'],
  permissions: ['recommendation.review', 'explanation-feedback.submit'],
};

// ---------------------------------------------------------------------------
// Fetch harness — routes by URL so identity, explanation, feedback and the
// decision log can answer independently.
// ---------------------------------------------------------------------------

const fetchMock = vi.fn();

interface Routes {
  identity?: CurrentUser;
  explain?: ExplanationResponse;
  explainStatus?: number;
  explainProblem?: { title?: string; detail?: string };
  /** Thrown instead of answering, i.e. a network failure. */
  explainNetworkError?: boolean;
  onPost?: (url: string, init: RequestInit) => Response | Promise<Response>;
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } });
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
          subjectKind: 'inventory-unit',
          subjectRef: 'STK-1',
          verdict: 'Useful',
          submittedAtUtc: '2026-07-24T09:05:00Z',
          caveat: CAVEAT,
        }),
      );
    }

    if (url.includes('/identity/me')) {
      return Promise.resolve(json(routes.identity ?? viewer));
    }

    if (url.includes('/predictions/explain')) {
      if (routes.explainNetworkError) return Promise.reject(new TypeError('Failed to fetch'));
      if (routes.explainStatus && routes.explainStatus >= 400) {
        return Promise.resolve(problem(routes.explainStatus, routes.explainProblem ?? {}));
      }
      return Promise.resolve(json(routes.explain ?? response()));
    }

    // The Decision Log lookup behind the footer.
    return Promise.resolve(json({ items: [], page: 1, pageSize: 5, totalCount: 0, statusCounts: {} }));
  });
}

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock);
  fetchMock.mockReset();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

// ---------------------------------------------------------------------------
// Harnesses
// ---------------------------------------------------------------------------

function withProviders(ui: React.ReactNode) {
  const rootRoute = createRootRoute();
  const index = createRoute({ getParentRoute: () => rootRoute, path: '/', component: () => <>{ui}</> });
  const decisions = createRoute({
    getParentRoute: () => rootRoute,
    path: '/decisions',
    component: () => <h1>Decision Log</h1>,
  });

  const router = createRouter({
    routeTree: rootRoute.addChildren([index, decisions]),
    history: createMemoryHistory({ initialEntries: ['/'] }),
  });

  const client = new QueryClient({
    // retryDelay 0 because the explanation query retries once on a *transport* failure (the reused
    // idempotency key is what makes that safe); the default exponential backoff would outlast every
    // assertion in this file.
    defaultOptions: {
      queries: { retry: false, gcTime: 0, retryDelay: 0 },
      mutations: { retry: false, retryDelay: 0 },
    },
  });

  return render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

/** A screen with one trigger, exactly as the nine real screens wire it. */
function Screen() {
  const drawer = useExplainabilityDrawer();

  return (
    <>
      <ExplainButton
        kind="inventory-unit"
        label="ES 350 ZX"
        onClick={() => drawer.open({ kind: 'inventory-unit', ref: 'STK-1' })}
      />
      <ExplainabilityDrawer subject={drawer.subject} onClose={drawer.close} />
    </>
  );
}

function renderScreen() {
  return withProviders(<Screen />);
}

async function openDrawer() {
  // `findBy`, not `getBy`: the memory router resolves its first match asynchronously, so the screen
  // is not yet in the DOM on the tick after render.
  fireEvent.click(await screen.findByRole('button', { name: /why this recommendation\? es 350 zx/i }));
  return screen.findByRole('dialog');
}

// ---------------------------------------------------------------------------

describe('ExplainabilityDrawer — geometry and header (V3-DS-006)', () => {
  it('opens as a modal dialog labelled by the subject', async () => {
    serve({});
    renderScreen();

    const dialog = await openDrawer();

    expect(dialog).toHaveAttribute('aria-modal', 'true');
    await waitFor(() => expect(dialog).toHaveAccessibleName('ES 350 ZX · STK-1'));
  });

  it("carries v3's explain geometry as a modifier rather than changing the shared drawer", async () => {
    serve({});
    renderScreen();

    const dialog = await openDrawer();

    // 474px / 94vw lives in .drawer--explain; the shared .drawer stays 480px / 92vw so every other
    // drawer in the app keeps the geometry it was designed at.
    expect(dialog).toHaveClass('drawer');
    expect(dialog).toHaveClass('drawer--explain');
  });

  it('shows the subject and its module beneath the gradient mark', async () => {
    serve({});
    renderScreen();
    await openDrawer();

    expect(await screen.findByText('ES 350 ZX · STK-1')).toBeInTheDocument();
    expect(screen.getByText(/Why this recommendation\? · Inventory Intelligence/)).toBeInTheDocument();
  });
});

describe('ExplainabilityDrawer — the eleven sections', () => {
  it('renders every section when the payload carries every part', async () => {
    serve({});
    renderScreen();
    await openDrawer();

    expect(await screen.findByRole('heading', { name: 'Recommendation' })).toBeInTheDocument();
    for (const name of [
      'Expected impact',
      'Confidence',
      'Top drivers',
      /Historical evidence/,
      'Assumptions',
      'Data lineage',
      'Model / rule information',
      'Ownership',
      'Was this useful?',
    ]) {
      expect(screen.getByRole('heading', { name })).toBeInTheDocument();
    }

    // Section 1 is the output label itself.
    expect(screen.getByText('Recommendation', { selector: '.ai-label' })).toBeInTheDocument();
  });

  it('omits the recommendation section entirely when there is none', async () => {
    serve({ explain: response({ explanation: explanation({ recommendation: null }) }) });
    renderScreen();
    await openDrawer();

    await screen.findByRole('heading', { name: 'Expected impact' });
    // Absent from the DOM, not an empty box.
    expect(screen.queryByRole('heading', { name: 'Recommendation' })).not.toBeInTheDocument();
  });

  it('omits the expected-impact section when there are no tiles', async () => {
    serve({ explain: response({ explanation: explanation({ impacts: [] }) }) });
    renderScreen();
    await openDrawer();

    await screen.findByRole('heading', { name: 'Confidence' });
    expect(screen.queryByRole('heading', { name: 'Expected impact' })).not.toBeInTheDocument();
  });

  it('renders impact values exactly as the server formatted them', async () => {
    serve({});
    renderScreen();
    await openDrawer();

    // Formatted once, invariantly, on the side that knows the unit. The browser never re-formats.
    expect(await screen.findByText('SAR 18.6K')).toBeInTheDocument();
    expect(screen.getByText('SAR 540.0K')).toBeInTheDocument();
  });

  it('omits the drivers section when there are none', async () => {
    serve({ explain: response({ explanation: explanation({ drivers: [] }) }) });
    renderScreen();
    await openDrawer();

    await screen.findByRole('heading', { name: 'Confidence' });
    expect(screen.queryByRole('heading', { name: 'Top drivers' })).not.toBeInTheDocument();
  });

  it('omits the evidence section when the subject has no series', async () => {
    serve({ explain: response({ explanation: explanation({ evidence: null }) }) });
    renderScreen();
    await openDrawer();

    await screen.findByRole('heading', { name: 'Assumptions' });
    // UC1, UC3, UC4, UC6 and UC7 have no chart on their screen to give the drawer. The section is
    // omitted rather than filled with a placeholder drawn from nothing.
    expect(screen.queryByRole('heading', { name: /Historical evidence/ })).not.toBeInTheDocument();
  });

  it('omits the assumptions section when the engine states none', async () => {
    serve({ explain: response({ explanation: explanation({ assumptions: [] }) }) });
    renderScreen();
    await openDrawer();

    await screen.findByRole('heading', { name: 'Data lineage' });
    expect(screen.queryByRole('heading', { name: 'Assumptions' })).not.toBeInTheDocument();
  });

  it('omits the lineage section when there are no nodes', async () => {
    serve({ explain: response({ explanation: explanation({ lineage: [] }) }) });
    renderScreen();
    await openDrawer();

    await screen.findByRole('heading', { name: 'Model / rule information' });
    expect(screen.queryByRole('heading', { name: 'Data lineage' })).not.toBeInTheDocument();
  });

  it('renders a lineage chip per node, labelled in words', async () => {
    serve({});
    renderScreen();
    await openDrawer();

    expect(await screen.findByText('Inventory workbook (inventory.json)')).toBeInTheDocument();
    expect(screen.getByText('Overstock risk model (UC5)')).toBeInTheDocument();
  });

  it('omits the model section when the payload carries no model info', async () => {
    serve({ explain: response({ explanation: explanation({ model: null }) }) });
    renderScreen();
    await openDrawer();

    await screen.findByRole('heading', { name: 'Data lineage' });
    expect(screen.queryByRole('heading', { name: 'Model / rule information' })).not.toBeInTheDocument();
  });

  it('renders the six model fields', async () => {
    serve({});
    renderScreen();
    await openDrawer();

    for (const field of ['Model', 'Version', 'Recalculated', 'Horizon', 'Validation', 'Error']) {
      expect(await screen.findByText(field)).toBeInTheDocument();
    }
  });

  it('omits the ownership section — and the workflow footer — when the subject is not a decision', async () => {
    serve({ explain: response({ explanation: explanation({ ownership: null }) }) });
    renderScreen();
    await openDrawer();

    await screen.findByRole('heading', { name: 'Model / rule information' });
    expect(screen.queryByRole('heading', { name: 'Ownership' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Decision Log/ })).not.toBeInTheDocument();
  });

  it('routes into the Decision Log from the footer when the subject is a decision', async () => {
    serve({});
    renderScreen();
    await openDrawer();

    // DecisionFooter owns the claim logic and the "no record exists yet" copy; the drawer reuses it
    // rather than growing a second answer to the same question.
    expect(await screen.findByRole('link', { name: /Decision Log/ })).toBeInTheDocument();
  });
});

describe('ExplainabilityDrawer — confidence is never invented', () => {
  it('renders the band and its reasons', async () => {
    serve({});
    renderScreen();
    await openDrawer();

    expect(await screen.findByText('High')).toBeInTheDocument();
    expect(screen.getByText('7.2 months cover here')).toBeInTheDocument();
  });

  it('renders the percentage when the engine computed one', async () => {
    serve({
      explain: response({
        explanation: explanation({
          confidence: { band: 'Medium', percent: 80, why: ['Back-tested on a 6-month hold-out.'] },
        }),
      }),
    });
    renderScreen();
    await openDrawer();

    expect(await screen.findByText('80%')).toBeInTheDocument();
  });

  it('omits the percentage when the engine computed none', async () => {
    serve({});
    renderScreen();
    await openDrawer();

    await screen.findByRole('heading', { name: 'Confidence' });
    expect(screen.queryByText(/^\d+%$/)).not.toBeInTheDocument();
  });

  it('omits the whole section when no band was computed, and never says "Medium"', async () => {
    // v3 defaults a missing confidence to "Medium". Asserting a band the engine never computed is a
    // correctness failure dressed as a default, so the section simply is not there.
    serve({ explain: response({ explanation: explanation({ confidence: null }) }) });
    const { container } = renderScreen();
    await openDrawer();

    await screen.findByRole('heading', { name: 'Top drivers' });
    expect(screen.queryByRole('heading', { name: 'Confidence' })).not.toBeInTheDocument();
    expect(container.textContent).not.toContain('Medium');
  });
});

describe('ExplainabilityDrawer — drivers are capped', () => {
  function manyDrivers(n: number) {
    return Array.from({ length: n }, (_, i) => ({
      label: `driver ${i + 1}`,
      detail: `detail ${i + 1}`,
    }));
  }

  it('renders at most eight, with a disclosure naming the total', async () => {
    serve({ explain: response({ explanation: explanation({ drivers: manyDrivers(12) }) }) });
    renderScreen();
    await openDrawer();

    expect(await screen.findByText('driver 8')).toBeInTheDocument();
    // An unbounded list turns the panel into a scroll trap.
    expect(screen.queryByText('driver 9')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Show all (12)' })).toBeInTheDocument();
  });

  it('reveals the rest when the disclosure is used, and folds them away again', async () => {
    serve({ explain: response({ explanation: explanation({ drivers: manyDrivers(12) }) }) });
    renderScreen();
    await openDrawer();

    fireEvent.click(await screen.findByRole('button', { name: 'Show all (12)' }));

    expect(await screen.findByText('driver 12')).toBeInTheDocument();
    const collapse = screen.getByRole('button', { name: 'Show fewer drivers' });
    expect(collapse).toHaveAttribute('aria-expanded', 'true');

    fireEvent.click(collapse);
    await waitFor(() => expect(screen.queryByText('driver 12')).not.toBeInTheDocument());
  });

  it('shows no disclosure when everything already fits', async () => {
    serve({ explain: response({ explanation: explanation({ drivers: manyDrivers(3) }) }) });
    renderScreen();
    await openDrawer();

    await screen.findByText('driver 3');
    expect(screen.queryByRole('button', { name: /Show all/ })).not.toBeInTheDocument();
  });
});

describe('ExplainabilityDrawer — all six states (§3.3)', () => {
  it('opens immediately and shows a loading state rather than appearing to hang', async () => {
    let release: (value: Response) => void = () => {};
    fetchMock.mockImplementation((input: string) => {
      const url = String(input);
      if (url.includes('/identity/me')) return Promise.resolve(json(viewer));
      if (url.includes('/predictions/explain')) {
        return new Promise<Response>((resolve) => {
          release = resolve;
        });
      }
      return Promise.resolve(json({ items: [], page: 1, pageSize: 5, totalCount: 0, statusCounts: {} }));
    });

    renderScreen();
    await openDrawer();

    expect(await screen.findByText('Assembling the explanation…')).toBeInTheDocument();

    release(json(response()));
    expect(await screen.findByText('ES 350 ZX · STK-1')).toBeInTheDocument();
  });

  it('states plainly that a figure carries no recorded explanation', async () => {
    serve({
      explainStatus: 404,
      explainProblem: {
        title: 'Nothing to explain',
        detail: "No 'inventory-unit' matching 'STK-1' carries a recorded explanation.",
      },
    });
    renderScreen();
    await openDrawer();

    expect(await screen.findByText('No recorded explanation')).toBeInTheDocument();
    expect(screen.getByText(/carries a recorded explanation/)).toBeInTheDocument();
  });

  it("renders the server's own explanation on an error", async () => {
    serve({
      explainStatus: 500,
      explainProblem: { title: 'Server error', detail: 'The analysis service is unavailable.' },
    });
    renderScreen();
    await openDrawer();

    expect(await screen.findByText('Could not load this explanation')).toBeInTheDocument();
    expect(screen.getByText('The analysis service is unavailable.')).toBeInTheDocument();
  });

  it('gives actionable text for a network failure rather than "Failed to fetch"', async () => {
    serve({ explainNetworkError: true });
    renderScreen();
    await openDrawer();

    expect(await screen.findByText(/Start the API host/)).toBeInTheDocument();
  });

  it('renders what arrived and says what is missing when a provider reported a gap', async () => {
    serve({
      explain: response({
        gaps: [{ area: 'inventory-unit', reason: 'This explanation could not be assembled.' }],
      }),
    });
    renderScreen();
    await openDrawer();

    // Partial: the explanation *and* the gap, never one silently standing for the other.
    expect(await screen.findByText('This explanation is incomplete.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Expected impact' })).toBeInTheDocument();
  });

  it('never lets a gap read as "there is nothing to explain here"', async () => {
    serve({
      explain: response({
        explanation: null,
        gaps: [{ area: 'inventory-unit', reason: 'This explanation could not be assembled.' }],
      }),
    });
    renderScreen();
    await openDrawer();

    expect(await screen.findByText('This explanation is incomplete.')).toBeInTheDocument();
    expect(screen.getByText(/could not be reached/)).toBeInTheDocument();
    expect(screen.getByText(/not the same as the figure having no explanation/)).toBeInTheDocument();
  });

  it('explains a permission denial instead of rendering an empty panel', async () => {
    serve({ explainStatus: 403, explainProblem: { title: 'Forbidden' } });
    renderScreen();
    await openDrawer();

    expect(await screen.findByText('You do not have access to this explanation')).toBeInTheDocument();
    expect(screen.getByText(/Ask an administrator/)).toBeInTheDocument();
  });
});

describe('ExplainabilityDrawer — feedback is recorded, and says it changes nothing', () => {
  it('carries v3’s caption stating that feedback does not retrain the model', async () => {
    serve({});
    renderScreen();
    await openDrawer();

    expect(await screen.findByText(CAVEAT)).toBeInTheDocument();
  });

  it('renders the four verdicts with v3’s wording', async () => {
    serve({});
    renderScreen();
    await openDrawer();

    const group = await screen.findByRole('group', { name: /was this explanation useful/i });
    for (const label of ['Useful', 'Needs review', 'Incorrect', 'Missing context']) {
      expect(within(group).getByRole('button', { name: label })).toBeInTheDocument();
    }
  });

  it('hides the verdicts and explains why when the caller lacks the submit permission (A.6)', async () => {
    // A viewer who can read the explanation but not record feedback — the same shape the server
    // enforces. Hiding the control is a courtesy; an explanatory line replaces it rather than a dead
    // button that 403s on click (the S6/S3 permission-rendering pattern).
    serve({ identity: { ...viewer, permissions: ['recommendation.review'] } });
    renderScreen();
    await openDrawer();

    // The "Was this useful?" section is still there, but as an explanation of the missing permission.
    expect(await screen.findByRole('heading', { name: 'Was this useful?' })).toBeInTheDocument();
    expect(screen.getByText(/explanation-feedback\.submit/i)).toBeInTheDocument();

    // The verdict controls are absent from the DOM, not merely disabled.
    expect(
      screen.queryByRole('group', { name: /was this explanation useful/i }),
    ).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Useful' })).not.toBeInTheDocument();
  });

  it('sends the verdict and the note to the server', async () => {
    // Captured rather than read back off the mock, so the request is inspected as a typed
    // RequestInit instead of through a cast.
    let sent: RequestInit | null = null;
    let calls = 0;

    serve({
      onPost: (_url, init) => {
        sent = init;
        calls += 1;
        return json({
          subjectKind: 'inventory-unit',
          subjectRef: 'STK-1',
          verdict: 'NeedsReview',
          submittedAtUtc: '2026-07-24T09:05:00Z',
          caveat: CAVEAT,
        });
      },
    });
    renderScreen();
    await openDrawer();

    fireEvent.change(await screen.findByLabelText(/Anything to add/), {
      target: { value: 'The cover figure looks stale.' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Needs review' }));

    await waitFor(() => expect(calls).toBe(1));

    const init = sent as unknown as RequestInit;
    expect(JSON.parse(init.body as string)).toMatchObject({
      kind: 'inventory-unit',
      ref: 'STK-1',
      verdict: 'NeedsReview',
      note: 'The cover figure looks stale.',
    });

    // Every state-changing write carries a key, so a double-click records one verdict, not two.
    expect((init.headers as Record<string, string>)['Idempotency-Key']).toBeTruthy();
  });

  it('announces the result politely once the server has accepted it', async () => {
    serve({});
    renderScreen();
    await openDrawer();

    fireEvent.click(await screen.findByRole('button', { name: 'Useful' }));

    const confirmation = await screen.findByText('Thank you — your feedback was recorded.');
    expect(confirmation).toHaveAttribute('role', 'status');
    expect(confirmation).toHaveAttribute('aria-live', 'polite');
  });

  it('renders a server refusal inline rather than swallowing it', async () => {
    serve({
      onPost: () =>
        problem(400, {
          title: 'Invalid request',
          detail: "'Splendid' is not a recognised verdict.",
        }),
    });
    renderScreen();
    await openDrawer();

    fireEvent.click(await screen.findByRole('button', { name: 'Useful' }));

    expect(await screen.findByText("'Splendid' is not a recognised verdict.")).toBeInTheDocument();
  });

  it('never marks itself submitted optimistically', async () => {
    let release: (value: Response) => void = () => {};
    serve({
      onPost: () =>
        new Promise<Response>((resolve) => {
          release = resolve;
        }),
    });
    renderScreen();
    await openDrawer();

    const useful = await screen.findByRole('button', { name: 'Useful' });
    fireEvent.click(useful);

    // In flight: the control does not claim success, because the server may still refuse.
    expect(await screen.findByText('Recording…')).toBeInTheDocument();
    expect(screen.queryByText('Thank you — your feedback was recorded.')).not.toBeInTheDocument();
    expect(useful).toHaveAttribute('aria-pressed', 'false');

    release(
      json({
        subjectKind: 'inventory-unit',
        subjectRef: 'STK-1',
        verdict: 'Useful',
        submittedAtUtc: '2026-07-24T09:05:00Z',
        caveat: CAVEAT,
      }),
    );

    expect(await screen.findByText('Thank you — your feedback was recorded.')).toBeInTheDocument();
  });

  it('marks the caller’s own current verdict, not the newest one from anyone', async () => {
    serve({
      explain: response({
        feedback: [
          {
            verdict: 'Incorrect',
            note: null,
            submittedBy: 'someone-else',
            submittedAtUtc: '2026-07-24T10:00:00Z',
          },
          {
            verdict: 'Useful',
            note: null,
            submittedBy: 'exec-1',
            submittedAtUtc: '2026-07-24T09:00:00Z',
          },
        ],
      }),
    });
    renderScreen();
    await openDrawer();

    const group = await screen.findByRole('group', { name: /was this explanation useful/i });
    expect(within(group).getByRole('button', { name: 'Useful' })).toHaveAttribute('aria-pressed', 'true');
    expect(within(group).getByRole('button', { name: 'Incorrect' })).toHaveAttribute('aria-pressed', 'false');
  });
});

describe('ExplainButton — the trigger every screen renders', () => {
  it('is a real button whose accessible name names the subject', async () => {
    serve({});
    renderScreen();

    // Not a bare icon: nine identical "info" buttons on one screen are unusable with a screen
    // reader, and an icon-only target misses the 44px touch minimum.
    const trigger = await screen.findByRole('button', { name: 'Why this recommendation? ES 350 ZX' });
    expect(trigger).toHaveAttribute('type', 'button');
  });

  it('returns focus to the invoking button when the drawer closes', async () => {
    serve({});
    renderScreen();

    const trigger = await screen.findByRole('button', { name: /why this recommendation\? es 350 zx/i });
    trigger.focus();
    fireEvent.click(trigger);

    await screen.findByRole('dialog');
    fireEvent.keyDown(window, { key: 'Escape' });

    await waitFor(() => expect(document.activeElement).toBe(trigger));
  });
});

// ---------------------------------------------------------------------------
// Stacked drawers — the Escape-priority chain (§3.4)
// ---------------------------------------------------------------------------

/**
 * The Decision Log's shape: a detail drawer with a trigger inside it that opens the explainability
 * drawer on top. Before the drawer stack existed, both drawers attached a `window` keydown listener
 * and one Escape closed both.
 */
function StackedScreen() {
  const [detailOpen, setDetailOpen] = useState(true);
  const drawer = useExplainabilityDrawer();

  return (
    <>
      <Drawer open={detailOpen} title="D-INV-1 · ES 350 ZX" onClose={() => setDetailOpen(false)}>
        <button type="button">Open evidence</button>
        <ExplainButton
          kind="inventory-unit"
          label="ES 350 ZX"
          onClick={() => drawer.open({ kind: 'inventory-unit', ref: 'STK-1' })}
        />
      </Drawer>
      <ExplainabilityDrawer subject={drawer.subject} onClose={drawer.close} />
    </>
  );
}

describe('Stacked drawers — Escape closes only the topmost', () => {
  async function openBoth() {
    withProviders(<StackedScreen />);

    const trigger = await screen.findByRole('button', { name: /why this recommendation\?/i });
    trigger.focus();
    fireEvent.click(trigger);

    await waitFor(() => expect(screen.getAllByRole('dialog')).toHaveLength(2));
    return trigger;
  }

  it('closes only the drawer on top', async () => {
    serve({});
    await openBoth();

    fireEvent.keyDown(window, { key: 'Escape' });

    await waitFor(() => expect(screen.getAllByRole('dialog')).toHaveLength(1));
    // The one beneath is still open and still the Decision Log's detail panel.
    expect(screen.getByRole('dialog')).toHaveAccessibleName('D-INV-1 · ES 350 ZX');
  });

  it('closes the drawer beneath only on a second Escape', async () => {
    serve({});
    await openBoth();

    fireEvent.keyDown(window, { key: 'Escape' });
    await waitFor(() => expect(screen.getAllByRole('dialog')).toHaveLength(1));

    fireEvent.keyDown(window, { key: 'Escape' });
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('restores focus into the drawer beneath, not to the page body', async () => {
    serve({});
    const trigger = await openBoth();

    fireEvent.keyDown(window, { key: 'Escape' });

    await waitFor(() => expect(document.activeElement).toBe(trigger));
    // The trigger lives inside the lower drawer, so focus really is back where the user left it.
    expect(screen.getByRole('dialog').contains(document.activeElement)).toBe(true);
  });

  it('traps Tab in the topmost drawer, so the one beneath cannot steal it', async () => {
    serve({});
    await openBoth();

    const [, top] = screen.getAllByRole('dialog');
    const inTop = within(top!).getAllByRole('button');
    const last = inTop[inTop.length - 1]!;

    last.focus();
    fireEvent.keyDown(window, { key: 'Tab' });

    await waitFor(() => expect(top!.contains(document.activeElement)).toBe(true));
  });

  it('stacks the top drawer above the one beneath', async () => {
    serve({});
    await openBoth();

    const overlays = document.querySelectorAll<HTMLElement>('.drawer-overlay');
    const [beneath, above] = [overlays[0]!, overlays[1]!];

    expect(Number(above.style.zIndex)).toBeGreaterThan(Number(beneath.style.zIndex));
  });
});
