import { fireEvent, render, screen, waitFor } from '@testing-library/react';
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
import type { ExplanationResponse } from '@/lib/api/explainability';

/**
 * V3-UC0x-002 — the explainability drawer is wired into every intelligence screen.
 *
 * One case per screen: the trigger exists, it names its subject, and using it opens a drawer titled
 * for **that** subject. The cockpit and the Decision Log are covered in their own suites, which
 * already have the fixtures for them.
 *
 * These are wiring tests, not layout tests. What they exist to catch is the failure that a
 * hand-check never finds: a screen where the button was added but points at the wrong subject
 * reference, so every row explains the same thing.
 */

// ---------------------------------------------------------------------------
// Minimal fixtures — enough for each screen to render its populated state.
// ---------------------------------------------------------------------------

const provenance = {
  provenance: 'synthetic-demo',
  note: 'Synthetic demo data derived deterministically from real vehicle sales.',
  generatedAtUtc: '2026-07-24T09:00:00Z',
};

const orderResponse = {
  scenario: {},
  items: [
    {
      model: 'ES 350',
      variant: 'ZX',
      chosenModel: 'Seasonal naive',
      wmape: 12.4,
      monthlyVelocity: 9.1,
      forecastDemand: 30,
      safetyStock: 9,
      available: 12,
      netRequirement: 27,
      recommendedQuantity: 30,
      overstockRisk: 'Low',
      understockRisk: 'High',
      confidence: 'High',
      rationale: 'Demand outpaces available supply.',
    },
  ],
  meta: { configurations: 1, totalRecommendedUnits: 30, generatedAtUtc: '2026-07-24T09:00:00Z' },
};

const procurementResponse = {
  scenario: {},
  items: [
    {
      model: 'Haval H9',
      variant: 'MX',
      demandMean: 6.2,
      demandStd: 2.1,
      leadTimeMonths: 2.4,
      safetyStock: 8,
      reorderPoint: 23,
      orderUpToLevel: 31,
      available: 10,
      recommendedQuantity: 21,
      rangeLow: 18,
      rangeHigh: 24,
      stockoutRisk: 'Medium',
      confidence: 'Medium',
      rationale: 'Lead-time demand exceeds available stock.',
    },
  ],
  meta: { configurations: 1, totalRecommendedUnits: 21, generatedAtUtc: '2026-07-24T09:00:00Z' },
};

const configRow = {
  model: 'ES 350',
  variant: 'ZX',
  colour: 'Pearl White',
  interior: 'Black',
  totalUnits: 84,
  recentVelocity: 3.4,
  decayPct: -12.5,
  trendDirection: 'declining',
  rotationClass: 'Fast',
  currentStock: 9,
  topRegionShare: 0.42,
  decayAlert: false,
  stockoutSuspected: false,
  isColdStart: false,
};

const inventoryRow = {
  stockId: 'STK-1',
  brand: 'Lexus',
  model: 'ES 350',
  variant: 'ZX',
  colour: 'Pearl White',
  location: 'Riyadh',
  inventoryAgeDays: 184,
  agingBand: 'Critical aging',
  velocity: 1.2,
  stockCover: 7.2,
  trendDirection: 'declining',
  riskScore: 71,
  riskBand: 'High',
  purchasePrice: 180_000,
  accumulatedHoldingCost: 18_615,
  recommendedAction: 'Transfer stock',
};

const inventoryDetail = {
  ...inventoryRow,
  recommendedAction: undefined,
  chassisNo: 'CH-1',
  interior: 'Black',
  type: 'Sedan',
  dateOfPurchase: '2025-12-28',
  dateOfManufacture: '2025-10-01',
  serviceDate: null,
  holdingCostPerDay: 51,
  leadTimeDays: 60,
  manufacturingAgeDays: 272,
  manufacturingBand: 'Aged',
  demandBasis: 'location-model-variant',
  demandConfidence: 'Medium',
  demandDetail: '1.2 units/month over 3 months',
  groupStock: 9,
  trendChangePct: -18.2,
  factors: [{ key: 'cover', label: 'estimated stock cover', points: 18, detail: '7.2 months of cover' }],
  recommendation: {
    action: 'Transfer stock',
    confidence: 'Medium',
    why: 'Riyadh holds elevated cover.',
    evidence: ['7.2 months cover here'],
    outcome: 'Rebalances stock toward demand.',
    assumptions: ['Transfer feasibility not modelled.'],
    destination: 'Jeddah',
    discountPct: null,
  },
};

const serviceModel = {
  model: 'Haval H9',
  totalEvents: 420,
  vehiclesInOperation: 180,
  eventsPerVehicle: 2.33,
  intensityIndex: 1.42,
  highIntensity: true,
  totalLaborHours: 910,
  laborHoursPerVehicle: 5.05,
  byMileageBand: [{ band: '0–10k', events: 120, eventsPerVehicle: 0.67 }],
  byTimeSinceSale: [{ band: '0–6 months', events: 90, eventsPerVehicle: 0.5 }],
  byServiceType: [
    { serviceType: 'Routine', events: 240, share: 0.57 },
    { serviceType: 'Repair', events: 100, share: 0.24 },
    { serviceType: 'Warranty', events: 60, share: 0.14 },
    { serviceType: 'Recall', events: 20, share: 0.05 },
  ],
  coverage: {
    vehiclesInOperation: 180,
    vehiclesWithEvents: 150,
    coverageRate: 0.83,
    monthsOfHistory: 18,
    reliabilityTier: 'High',
  },
  correlation: {
    lag0: 0.21,
    best: 0.64,
    bestLagMonths: 6,
    interpretation: 'Service volume follows sales with a six-month lag.',
  },
};

const partRow = {
  partNumber: 'P-1001',
  name: 'Engine Control Module',
  category: 'Electrical',
  location: 'Riyadh',
  demandClass: 'Intermittent',
  method: 'Croston',
  predictedMonthlyDemand: 4.2,
  stockingRangeLow: 3,
  stockingRangeHigh: 7,
  currentStock: 2,
  inboundStock: 0,
  leadTimeDays: 45,
  reorderPoint: 6,
  stockoutRisk: 'High',
  holdingRisk: 'Low',
  confidence: 'Medium',
  insufficientData: false,
};

const partDetail = {
  scenario: { serviceLevel: 0.95, reviewPeriodMonths: 1 },
  national: {
    partNumber: 'P-1001',
    name: 'Engine Control Module',
    category: 'Electrical',
    class: 'Intermittent',
    method: 'Croston',
    adi: 2.4,
    cv2: 0.31,
    nonZeroPeriods: 8,
    periods: 18,
    predictedMonthlyDemand: 4.2,
    monthlyRangeLow: 3,
    monthlyRangeHigh: 7,
    leadTimeMonths: 1.5,
    leadTimeDemand: 6.3,
    safetyStock: 3.1,
    reorderPoint: 9.4,
    orderUpToLevel: 13.6,
    available: 2,
    recommendedQuantity: 12,
    stockingRangeLow: 10,
    stockingRangeHigh: 14,
    stockoutRisk: 'High',
    holdingRisk: 'Low',
    confidence: 'Medium',
    insufficientData: false,
    action: 'Increase stock',
    rationale: 'Lead-time demand exceeds stock on hand.',
    comparison: { ses: 3.9, croston: 4.2, sba: 4.0, tsb: 4.1 },
  },
  usageHistory: [{ month: '2026-05', quantity: 3 }],
  byLocation: [partRow],
  compatibleModels: ['Haval H9'],
  rolledUpSupersessions: [],
  supersededByPartNumber: null,
  meta: provenance,
};

function explanationFor(title: string, module: string): ExplanationResponse {
  return {
    subjectKind: 'inventory-unit',
    subjectRef: 'STK-1',
    explanation: {
      title,
      module,
      label: 'recommendation',
      recommendation: null,
      impacts: [],
      confidence: null,
      drivers: [],
      evidence: null,
      assumptions: [],
      lineage: [{ label: 'Sales workbook', kind: 'workbook' }],
      model: null,
      ownership: null,
      isDemoData: false,
    },
    gaps: [],
    feedback: [],
    feedbackCaveat: 'Recorded in this analytics platform only.',
    generatedAtUtc: '2026-07-24T09:00:00Z',
  };
}

// ---------------------------------------------------------------------------
// Harness
// ---------------------------------------------------------------------------

const fetchMock = vi.fn();

function json(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } });
}

/**
 * Routes by URL. The explanation is captured so each case can assert the drawer was opened for the
 * subject the screen claimed, not merely that *a* drawer opened.
 */
let requestedExplainUrl: string | null = null;

function serve(explainTitle: string, explainModule: string) {
  requestedExplainUrl = null;

  fetchMock.mockImplementation((input: string) => {
    const url = String(input);

    if (url.includes('/predictions/explain')) {
      requestedExplainUrl = url;
      return Promise.resolve(json(explanationFor(explainTitle, explainModule)));
    }

    if (url.includes('/identity/me')) {
      return Promise.resolve(
        json({
          isAuthenticated: true,
          subjectId: 'exec-1',
          displayName: 'An Executive',
          roles: ['Executive'],
          permissions: ['recommendation.review', 'explanation-feedback.submit'],
        }),
      );
    }

    // --- UC1 / UC4
    if (url.includes('/recommendations/order-optimisation')) return Promise.resolve(json(orderResponse));
    if (url.includes('/procurement/recommendations')) return Promise.resolve(json(procurementResponse));

    // --- UC2
    if (url.includes('/forecasting/filter-options')) {
      return Promise.resolve(
        json({
          brands: ['Lexus'],
          models: ['ES 350'],
          variants: ['ZX'],
          types: ['Sedan'],
          locations: ['Riyadh'],
          colours: ['Pearl White'],
          interiors: ['Black'],
          firstMonth: '2024-01',
          lastMonth: '2026-06',
        }),
      );
    }
    if (url.includes('/forecasting/accuracy-by')) {
      return Promise.resolve(
        json({ dimension: 'model', rows: [], meta: { monthsCovered: 30, historicalUnits: 900, generatedAtUtc: '' } }),
      );
    }
    if (url.includes('/forecasting/forecast')) {
      return Promise.resolve(
        json({
          forecast: {
            history: [
              { month: '2026-05', label: 'May', value: 20, isHold: false },
              { month: '2026-06', label: 'Jun', value: 24, isHold: true },
            ],
            backtest: [{ month: '2026-06', label: 'Jun', actual: 24, forecast: 22 }],
            future: [{ month: '2026-07', label: 'Jul', value: 25, lo: 20, hi: 30 }],
            futureSum: 25,
            chosen: 'snaive',
            chosenName: 'Seasonal naive',
            best: 'snaive',
            methods: [{ key: 'snaive', name: 'Seasonal naive', wmape: 12.4, bias: 1.2, mae: 2.1, rmse: 3, isBest: true, isChosen: true }],
            accuracy: { wmape: 12.4, mae: 2.1, rmse: 3, bias: 1.2, biasAbs: 1.2, mape: 12, overPct: 50, underPct: 50, n: 6 },
            sigma: 2.4,
            holdout: 6,
            horizon: 6,
            trainN: 24,
            totalN: 30,
            histUnits: 900,
            lastMonth: '2026-06',
            explanation: { points: ['Demand is holding.'], recent3: 22, prior12: 20, changePct: 10 },
          },
          meta: { monthsCovered: 30, historicalUnits: 900, generatedAtUtc: '2026-07-24T09:00:00Z' },
        }),
      );
    }

    // --- UC3
    if (url.includes('/config-demand/filter-options')) {
      return Promise.resolve(
        json({ models: ['ES 350'], variants: ['ZX'], colours: ['Pearl White'], interiors: ['Black'], rotations: ['Fast'] }),
      );
    }
    if (url.includes('/config-demand/summary')) {
      return Promise.resolve(
        json({
          summary: {
            configurations: 1,
            totalUnits: 84,
            fastCount: 1,
            mediumCount: 0,
            slowCount: 0,
            deadCount: 0,
            decayAlerts: 0,
            coldStart: 0,
            stockoutSuspected: 0,
            byRotation: [{ key: 'Fast', configurations: 1, units: 84 }],
          },
          meta: { totalConfigurations: 1, filteredConfigurations: 1, generatedAtUtc: '2026-07-24T09:00:00Z' },
        }),
      );
    }
    if (url.includes('/config-demand/configs')) {
      return Promise.resolve(
        json({
          items: [configRow],
          page: 1,
          pageSize: 25,
          totalCount: 1,
          meta: { totalConfigurations: 1, filteredConfigurations: 1, generatedAtUtc: '2026-07-24T09:00:00Z' },
        }),
      );
    }

    // --- UC5
    if (url.includes('/inventory/filter-options')) {
      return Promise.resolve(
        json({
          brands: ['Lexus'],
          models: ['ES 350'],
          variants: ['ZX'],
          types: ['Sedan'],
          locations: ['Riyadh'],
          colours: ['Pearl White'],
          interiors: ['Black'],
          riskBands: ['High'],
        }),
      );
    }
    if (/\/inventory\/items\/[^?]+/.test(url)) return Promise.resolve(json(inventoryDetail));
    if (url.includes('/inventory/items')) {
      return Promise.resolve(
        json({
          items: [inventoryRow],
          page: 1,
          pageSize: 25,
          totalCount: 1,
          meta: { analysisDate: '2026-06-30', totalUnits: 1, filteredUnits: 1, generatedAtUtc: '' },
        }),
      );
    }
    if (url.includes('/inventory/summary')) {
      return Promise.resolve(
        json({
          summary: {
            count: 1,
            value: 180_000,
            accumulatedHoldingCost: 18_615,
            dailyHoldingCost: 51,
            averageInventoryAge: 184,
            averageManufacturingAge: 272,
            averageLeadTime: 60,
            highRiskValue: 180_000,
            criticalValue: 0,
            criticalCount: 0,
            highCount: 1,
            decliningValue: 180_000,
            transferCount: 1,
            promotionCount: 0,
            discountCount: 0,
            pauseCount: 0,
            byRisk: [{ key: 'High', units: 1, value: 180_000 }],
            byAging: [{ key: 'Critical aging', units: 1, value: 180_000 }],
            byManufacturing: [{ key: 'Aged', units: 1, value: 180_000 }],
            byLocation: [{ key: 'Riyadh', units: 1, value: 180_000, accumulatedHoldingCost: 18_615 }],
            byModel: [{ key: 'ES 350', units: 1, value: 180_000, accumulatedHoldingCost: 18_615 }],
            byVariant: [{ key: 'ZX', units: 1, value: 180_000, accumulatedHoldingCost: 18_615 }],
            byBrand: [{ key: 'Lexus', units: 1, value: 180_000, accumulatedHoldingCost: 18_615 }],
            byColour: [{ key: 'Pearl White', units: 1, value: 180_000, accumulatedHoldingCost: 18_615 }],
            byInterior: [{ key: 'Black', units: 1, value: 180_000, accumulatedHoldingCost: 18_615 }],
          },
          meta: { analysisDate: '2026-06-30', totalUnits: 1, filteredUnits: 1, generatedAtUtc: '' },
        }),
      );
    }

    // --- UC6
    if (url.includes('/after-sales/filter-options')) {
      return Promise.resolve(
        json({ models: ['Haval H9'], variants: ['MX'], locations: ['Riyadh'], mileageBands: ['0–10k'], serviceTypes: ['Routine'] }),
      );
    }
    if (/\/service-intensity\/model\//.test(url)) {
      return Promise.resolve(json({ model: serviceModel, meta: provenance }));
    }
    if (url.includes('/service-intensity/by-model')) {
      return Promise.resolve(
        json({
          page: {
            items: [
              {
                model: serviceModel.model,
                totalEvents: serviceModel.totalEvents,
                vehiclesInOperation: serviceModel.vehiclesInOperation,
                eventsPerVehicle: serviceModel.eventsPerVehicle,
                intensityIndex: serviceModel.intensityIndex,
                highIntensity: serviceModel.highIntensity,
                totalLaborHours: serviceModel.totalLaborHours,
                laborHoursPerVehicle: serviceModel.laborHoursPerVehicle,
                coverageRate: serviceModel.coverage.coverageRate,
                monthsOfHistory: serviceModel.coverage.monthsOfHistory,
                reliabilityTier: serviceModel.coverage.reliabilityTier,
              },
            ],
            page: 1,
            pageSize: 25,
            totalCount: 1,
          },
          meta: provenance,
        }),
      );
    }
    if (url.includes('/service-intensity/summary')) {
      return Promise.resolve(
        json({
          summary: {
            modelsTracked: 1,
            totalEvents: 420,
            totalVehiclesInOperation: 180,
            fleetEventsPerVehicle: 2.33,
            averageIntensityIndex: 1,
            highIntensityModels: 1,
            overallCoverageRate: 0.83,
            monthsOfHistory: 18,
          },
          meta: provenance,
        }),
      );
    }

    // --- UC7
    if (url.includes('/spare-parts/filter-options')) {
      return Promise.resolve(json({ categories: ['Electrical'], models: ['Haval H9'], demandClasses: ['Intermittent'] }));
    }
    if (/\/demand\/part\//.test(url)) return Promise.resolve(json(partDetail));
    if (url.includes('/demand/parts')) {
      return Promise.resolve(
        json({
          scenario: { serviceLevel: 0.95, reviewPeriodMonths: 1 },
          page: { items: [partRow], page: 1, pageSize: 25, totalCount: 1 },
          meta: provenance,
        }),
      );
    }
    if (url.includes('/demand/summary')) {
      return Promise.resolve(
        json({
          scenario: { serviceLevel: 0.95, reviewPeriodMonths: 1 },
          summary: {
            distinctParts: 1,
            stockingPoints: 1,
            lowDataPoints: 0,
            atRiskPoints: 1,
            predictedMonthlyDemandTotal: 4.2,
            byDemandClass: [{ demandClass: 'Intermittent', count: 1 }],
          },
          meta: provenance,
        }),
      );
    }

    // The Decision Log lookup behind the drawer footer.
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

/** Opens the trigger and asserts the drawer is titled for the subject the screen asked about. */
async function expectOpensDrawerFor(triggerName: RegExp, expectedRef: string, drawerTitle: string) {
  fireEvent.click(await screen.findByRole('button', { name: triggerName }));

  expect(await screen.findByText(drawerTitle)).toBeInTheDocument();

  await waitFor(() => expect(requestedExplainUrl).not.toBeNull());

  // Parsed rather than string-matched: URLSearchParams encodes a space as "+", so a raw substring
  // check would pass for the wrong reason (or fail for the right one) depending on the subject.
  const params = new URLSearchParams(requestedExplainUrl!.split('?')[1]);
  expect(params.get('ref')).toBe(expectedRef);
}

// ---------------------------------------------------------------------------

describe('V3-UC0x-002 — the drawer is wired into every intelligence screen', () => {
  it('UC1 — an order row explains its configuration', async () => {
    serve('ES 350 ZX', 'Order Optimisation');
    renderScreen(OrderOptimisation);

    await expectOpensDrawerFor(/why this recommendation\? es 350 zx/i, 'ES 350|ZX', 'ES 350 ZX');
  });

  it('UC2 — the forecast scope header explains the scope, not a row', async () => {
    serve('Total business', 'Forecast Accuracy');
    renderScreen(SalesForecasting);

    // "|" is the unfiltered scope: no model, no location. The screen must send exactly that rather
    // than inventing a default model.
    await expectOpensDrawerFor(/why this recommendation\? total business/i, '|', 'Total business');
  });

  it('UC3 — a configuration row explains its four-part subject reference', async () => {
    serve('ES 350 ZX · Pearl White · Black', 'Configuration Insights');
    renderScreen(ConfigurationDemand);

    await expectOpensDrawerFor(
      /why this recommendation\? es 350 zx pearl white/i,
      'ES 350|ZX|Pearl White|Black',
      'ES 350 ZX · Pearl White · Black',
    );
  });

  it('UC4 — a procurement row explains its configuration', async () => {
    serve('Haval H9 MX', 'Procurement Optimisation');
    renderScreen(Procurement);

    await expectOpensDrawerFor(/why this recommendation\? haval h9 mx/i, 'Haval H9|MX', 'Haval H9 MX');
  });

  it('UC5 — the unit detail drawer explains the unit, stacked over itself', async () => {
    serve('ES 350 ZX · STK-1', 'Inventory Intelligence');
    renderScreen(InventoryIntelligence);

    // Open the unit's own detail drawer first; the explanation opens on top of it.
    fireEvent.click(await screen.findByRole('row', { name: /STK-1/ }));

    await expectOpensDrawerFor(/why this recommendation\? es 350 zx · stk-1/i, 'STK-1', 'ES 350 ZX · STK-1');
    expect(screen.getAllByRole('dialog')).toHaveLength(2);
  });

  it('UC6 — the model detail drawer explains the model', async () => {
    serve('Haval H9 — service intensity', 'Sales ↔ Service Correlation');
    renderScreen(AfterSales);

    // The table row, not the filter's <option> of the same name.
    fireEvent.click(await screen.findByRole('row', { name: /Haval H9/ }));

    await expectOpensDrawerFor(
      /why this recommendation\? haval h9/i,
      'Haval H9',
      'Haval H9 — service intensity',
    );
  });

  it('UC7 — the part detail drawer explains the part', async () => {
    serve('P-1001 · Engine Control Module', 'Spare Parts Prediction');
    renderScreen(SpareParts);

    fireEvent.click(await screen.findByRole('row', { name: /P-1001/ }));

    await expectOpensDrawerFor(
      /why this recommendation\? p-1001 engine control module/i,
      'P-1001',
      'P-1001 · Engine Control Module',
    );
  });
});
