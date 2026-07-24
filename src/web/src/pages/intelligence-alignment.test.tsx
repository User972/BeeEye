import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import {
  RouterProvider,
  createMemoryHistory,
  createRootRoute,
  createRoute,
  createRouter,
} from '@tanstack/react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import OrderOptimisation from './order-optimisation';
import SalesForecasting from './sales-forecasting';
import ConfigurationDemand from './configuration-demand';
import Procurement from './procurement';
import InventoryIntelligence from './inventory-intelligence';
import AfterSales from './after-sales';
import SpareParts from './spare-parts';

/**
 * V3-UC01..07-001 — S8 intelligence-screen alignment. Two things this suite guards that a visual
 * baseline cannot: (1) every one of the seven intelligence screens now carries the `wireframed`
 * PageHeader marker, and (2) the realigned screens actually surface the v3 layout elements added in S8
 * (the UC4 supplier synthetic-data banner, the distribution card and existing-field columns) — using
 * data the API already returns, with no number changed. Behavioural wiring (filters/sort/paging/drawer/
 * ExplainButton) is covered per screen in explainability-wiring.test.tsx.
 */

const fetchMock = vi.fn();

function json(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } });
}

function renderScreen(Component: () => React.ReactNode) {
  const rootRoute = createRootRoute();
  const index = createRoute({ getParentRoute: () => rootRoute, path: '/', component: Component });
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
    defaultOptions: { queries: { retry: false, gcTime: 0, retryDelay: 0 }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

const provenance = { provenance: 'synthetic-demo', note: 'Synthetic demo data.', generatedAtUtc: '2026-07-24T09:00:00Z' };

const orderResponse = {
  scenario: {},
  items: [{
    model: 'ES 350', variant: 'ZX', chosenModel: 'Seasonal naive', wmape: 12.4, monthlyVelocity: 9.1,
    forecastDemand: 30, safetyStock: 9, available: 12, netRequirement: 27, recommendedQuantity: 30,
    overstockRisk: 'Low', understockRisk: 'High', confidence: 'High', rationale: 'Demand outpaces supply.',
  }],
  meta: { configurations: 1, totalRecommendedUnits: 30, generatedAtUtc: '2026-07-24T09:00:00Z' },
};

const procurementResponse = {
  scenario: {},
  items: [{
    model: 'Haval H9', variant: 'MX', demandMean: 6.2, demandStd: 2.1, leadTimeMonths: 2.4, safetyStock: 8,
    reorderPoint: 23, orderUpToLevel: 31, available: 10, recommendedQuantity: 21, rangeLow: 18, rangeHigh: 24,
    stockoutRisk: 'Medium', confidence: 'Medium', rationale: 'Lead-time demand exceeds stock.',
  }],
  meta: { configurations: 1, totalRecommendedUnits: 21, generatedAtUtc: '2026-07-24T09:00:00Z' },
};

const configSummary = {
  summary: {
    configurations: 1, totalUnits: 84, fastCount: 1, mediumCount: 0, slowCount: 0, deadCount: 0,
    decayAlerts: 0, coldStart: 2, stockoutSuspected: 0, byRotation: [{ key: 'Fast', configurations: 1, units: 84 }],
  },
  meta: { totalConfigurations: 1, filteredConfigurations: 1, generatedAtUtc: '2026-07-24T09:00:00Z' },
};

const configList = {
  items: [{
    model: 'ES 350', variant: 'ZX', colour: 'Pearl White', interior: 'Black', totalUnits: 84,
    recentVelocity: 3.4, decayPct: -12.5, trendDirection: 'declining', rotationClass: 'Fast', currentStock: 9,
    topRegionShare: 0.42, decayAlert: false, stockoutSuspected: false, isColdStart: false,
  }],
  page: 1, pageSize: 25, totalCount: 1,
  meta: { totalConfigurations: 1, filteredConfigurations: 1, generatedAtUtc: '' },
};

const afterSalesSummary = {
  summary: {
    modelsTracked: 1, totalEvents: 420, totalVehiclesInOperation: 180, fleetEventsPerVehicle: 2.33,
    averageIntensityIndex: 1.42, highIntensityModels: 1, overallCoverageRate: 0.83, monthsOfHistory: 18,
  },
  meta: provenance,
};

const afterSalesByModel = {
  page: {
    items: [{
      model: 'Haval H9', totalEvents: 420, vehiclesInOperation: 180, eventsPerVehicle: 2.33, intensityIndex: 1.42,
      highIntensity: true, totalLaborHours: 910, laborHoursPerVehicle: 5.05, coverageRate: 0.83,
      monthsOfHistory: 18, reliabilityTier: 'High',
    }],
    page: 1, pageSize: 20, totalCount: 1,
  },
  meta: provenance,
};

const partsSummary = {
  scenario: { serviceLevel: 0.95, reviewPeriodMonths: 1 },
  summary: {
    distinctParts: 1, stockingPoints: 1, lowDataPoints: 0, atRiskPoints: 1, predictedMonthlyDemandTotal: 4.2,
    byDemandClass: [{ demandClass: 'Intermittent', count: 1 }],
  },
  meta: provenance,
};

const partsList = {
  scenario: { serviceLevel: 0.95, reviewPeriodMonths: 1 },
  page: {
    items: [{
      partNumber: 'P-1001', name: 'Engine Control Module', category: 'Electrical', location: 'Riyadh',
      demandClass: 'Intermittent', method: 'Croston', predictedMonthlyDemand: 4.2, stockingRangeLow: 3,
      stockingRangeHigh: 7, currentStock: 2, inboundStock: 0, leadTimeDays: 45, reorderPoint: 6,
      stockoutRisk: 'High', holdingRisk: 'Low', confidence: 'Medium', insufficientData: false,
    }],
    page: 1, pageSize: 25, totalCount: 1,
  },
  meta: provenance,
};

function serve() {
  fetchMock.mockImplementation((input: string) => {
    const url = String(input);
    if (url.includes('/identity/me')) {
      return Promise.resolve(json({ isAuthenticated: true, subjectId: 'x', displayName: 'X', roles: ['Analyst'], permissions: [] }));
    }
    if (url.includes('/recommendations/order-optimisation')) return Promise.resolve(json(orderResponse));
    if (url.includes('/procurement/recommendations')) return Promise.resolve(json(procurementResponse));
    if (url.includes('/config-demand/filter-options')) {
      return Promise.resolve(json({ models: ['ES 350'], variants: ['ZX'], colours: ['Pearl White'], interiors: ['Black'], rotations: ['Fast'] }));
    }
    if (url.includes('/config-demand/summary')) return Promise.resolve(json(configSummary));
    if (url.includes('/config-demand/configs')) return Promise.resolve(json(configList));
    if (url.includes('/after-sales/filter-options')) {
      return Promise.resolve(json({ models: ['Haval H9'], variants: ['MX'], locations: ['Riyadh'], mileageBands: ['0–10k'], serviceTypes: ['Routine'] }));
    }
    if (url.includes('/service-intensity/by-model')) return Promise.resolve(json(afterSalesByModel));
    if (url.includes('/service-intensity/summary')) return Promise.resolve(json(afterSalesSummary));
    if (url.includes('/spare-parts/filter-options')) {
      return Promise.resolve(json({ categories: ['Electrical'], models: ['Haval H9'], demandClasses: ['Intermittent'] }));
    }
    if (url.includes('/demand/parts')) return Promise.resolve(json(partsList));
    if (url.includes('/demand/summary')) return Promise.resolve(json(partsSummary));
    return Promise.resolve(json({ items: [], page: 1, pageSize: 5, totalCount: 0, statusCounts: {} }));
  });
}

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock);
  fetchMock.mockReset();
});
afterEach(() => vi.unstubAllGlobals());

describe('S8 — every intelligence screen carries the wireframed marker', () => {
  const screens: [string, () => React.ReactNode][] = [
    ['UC1', OrderOptimisation],
    ['UC2', SalesForecasting],
    ['UC3', ConfigurationDemand],
    ['UC4', Procurement],
    ['UC5', InventoryIntelligence],
    ['UC6', AfterSales],
    ['UC7', SpareParts],
  ];

  it.each(screens)('%s renders the Wireframed PageHeader marker', async (_uc, Component) => {
    // The marker renders outside the loading/error conditional, so a never-resolving fetch is enough:
    // it holds the page in its loading state while the header (and its marker) mount. findByText waits
    // for the router to mount the route component (it does so asynchronously).
    fetchMock.mockReturnValue(new Promise(() => {}));
    renderScreen(Component);
    expect(await screen.findByText('Wireframed')).toBeInTheDocument();
    expect(screen.queryByText('Designed from shared language')).not.toBeInTheDocument();
  });
});

describe('S8 — realigned screens surface their v3 layout elements (existing data only)', () => {
  beforeEach(() => serve());

  it('UC1 adds a recommended-order distribution card and velocity/chosen-model columns', async () => {
    renderScreen(OrderOptimisation);
    expect(await screen.findByText('Recommended order by configuration')).toBeInTheDocument();
    expect(screen.getByText('Velocity')).toBeInTheDocument();
    expect(screen.getByText('Chosen model')).toBeInTheDocument();
  });

  it('UC3 adds a Cold-starts tile and a Trend column', async () => {
    renderScreen(ConfigurationDemand);
    expect(await screen.findByText('Cold starts')).toBeInTheDocument();
    expect(screen.getByText('Trend')).toBeInTheDocument();
  });

  it('UC4 renders the supplier synthetic-data banner and the order-up-to / min·base·max columns', async () => {
    renderScreen(Procurement);
    const note = await screen.findByRole('note', { name: /demo supplier data notice/i });
    expect(note).toHaveTextContent(/supplier master and purchase-order history/i);
    expect(note).toHaveTextContent(/not.*Oracle Fusion/i);
    expect(await screen.findByText('Order-up-to')).toBeInTheDocument();
    expect(screen.getByText(/Range \(min · base · max\)/)).toBeInTheDocument();
  });

  it('UC6 adds a vehicles-in-operation tile and keeps its after-sales synthetic banner', async () => {
    renderScreen(AfterSales);
    expect(await screen.findByText('Vehicles in operation')).toBeInTheDocument();
    expect(screen.getByRole('note', { name: /synthetic demo data notice/i })).toBeInTheDocument();
  });

  it('UC7 adds a Reorder column and keeps its spare-parts synthetic banner', async () => {
    renderScreen(SpareParts);
    expect(await screen.findByText('Reorder')).toBeInTheDocument();
    expect(screen.getByRole('note', { name: /synthetic demo data notice/i })).toBeInTheDocument();
  });
});
