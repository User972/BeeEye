/**
 * Single source of truth for the application's screens. Drives the navigation
 * rail, the router, and each use-case page's scaffold content.
 *
 * The information architecture follows the v3 high-fidelity designs
 * (`docs/wireframes-v3/Meridian BI.dc.html`, `navGroupsDef()`): six navigation
 * groups, each with an optional delivery-phase label. See
 * `docs/implementation/v3-design-inventory.md` §1 for the full v3 inventory and
 * `docs/implementation/v3-design-traceability.md` for requirement IDs.
 *
 * Registry invariant: a nav item is listed here only when a real screen exists
 * for it. v3 groups also contain screens that are not built yet (Executive
 * Overview, Ask Decision Intelligence, Reports & Exports, Data Ingestion,
 * Methodology, Integration Blueprint); those are added by their own vertical
 * slices so the rail never shows a link that goes nowhere. The Decision Log
 * joined the registry with S6; Data Health and Model & Data Lineage joined with
 * S7, when the screens behind them became real.
 */

/** v3 navigation groups, in v3's display order. */
export type NavGroupId =
  | 'executive'
  | 'sales'
  | 'supply'
  | 'after-sales'
  | 'governance'
  | 'platform';

export interface NavGroup {
  id: NavGroupId;
  label: string;
  /** Subtle delivery-phase label shown beside the group heading in v3. */
  phase?: string;
}

export interface NavItem {
  /** Stable id; matches the lazy page module file name under src/pages. */
  id: string;
  path: string;
  label: string;
  /** Material Symbols Outlined icon name. */
  icon: string;
  group: NavGroupId;
  /** Use-case code, e.g. "UC5". Undefined for platform screens. */
  useCase?: string;
  /** Primary API module route prefix this screen reads from. */
  moduleRoute?: string;
  summary: string;
  /** Whether a wireframe already exists for this screen (UC2, UC5). */
  wireframed: boolean;
  /** Headline capabilities, shown on the page and traceable to the use-case spec. */
  capabilities: string[];
}

/**
 * Groups in v3's order. Phase labels are taken verbatim from the v3
 * `navGroupsDef()`; Governance and Platform carry no phase label there.
 */
export const navGroups: NavGroup[] = [
  { id: 'executive', label: 'Executive', phase: 'Phase 5' },
  { id: 'sales', label: 'Sales Intelligence', phase: 'Phase 1' },
  { id: 'supply', label: 'Supply Intelligence', phase: 'Phase 2–3' },
  { id: 'after-sales', label: 'After-Sales Intelligence', phase: 'Phase 4' },
  { id: 'governance', label: 'Governance' },
  { id: 'platform', label: 'Platform' },
];

export const navItems: NavItem[] = [
  {
    id: 'executive-cockpit',
    path: '/',
    label: 'Decision Cockpit',
    icon: 'dashboard',
    group: 'executive',
    useCase: 'UC8',
    moduleRoute: 'executive-insights',
    summary: 'Material exceptions from every module, prioritised for decision.',
    wireframed: true,
    capabilities: [
      'Aggregate material exceptions across all modules',
      'Prioritise by financial exposure, risk, urgency and confidence',
      'Assign owners, due dates and track decision status',
      'Executive summary linked to underlying evidence',
    ],
  },
  {
    id: 'order-optimisation',
    path: '/order-optimisation',
    label: 'Order Optimisation',
    icon: 'shopping_cart_checkout',
    group: 'sales',
    useCase: 'UC1',
    moduleRoute: 'recommendations',
    summary: 'Monthly vehicle order quantities balancing demand and constraints.',
    wireframed: false,
    capabilities: [
      'Separate demand forecast from business constraints and optimisation',
      'Account for current + inbound inventory, orders, lead times, MOQ, allocation',
      'Recommend order quantities by configuration with confidence',
      'Compare scenarios; review, accept, reject or amend',
    ],
  },
  {
    id: 'sales-forecasting',
    path: '/forecasting',
    label: 'Forecast Accuracy',
    icon: 'trending_up',
    group: 'sales',
    useCase: 'UC2',
    moduleRoute: 'forecasting',
    summary: 'Forecast accuracy, bias detection and correction factors.',
    wireframed: true,
    capabilities: [
      'Compare versioned forecasts with actuals (WMAPE, MAE, RMSE, bias)',
      'Detect consistent optimism / conservatism and repeated bias',
      'Accuracy by horizon, product hierarchy and region',
      'Recommend explainable correction factors',
    ],
  },
  {
    id: 'configuration-demand',
    path: '/configuration-demand',
    label: 'Configuration Insights',
    icon: 'grid_view',
    group: 'sales',
    useCase: 'UC3',
    moduleRoute: 'sales-actuals',
    summary: 'Configuration-level demand, clusters and dead-stock signals.',
    wireframed: false,
    capabilities: [
      'Demand heatmaps across model / variant / colour / trim / options',
      'Identify high-demand clusters and demand decay',
      'Distinguish genuine low demand from stockout-suppressed demand',
      'Configurable demand-decay alerts and drill-down',
    ],
  },
  {
    id: 'procurement',
    path: '/procurement',
    label: 'Procurement Optimisation',
    icon: 'local_shipping',
    group: 'supply',
    useCase: 'UC4',
    moduleRoute: 'procurement',
    summary: 'Procurement quantities balancing demand, lead time and cost.',
    wireframed: false,
    capabilities: [
      'Safety stock for configurable target service levels',
      'Lead-time variability, MOQ, order multiples and open POs',
      'Recommend procurement ranges, never ignoring inbound inventory',
      'Explain every major input; store approval or rejection',
    ],
  },
  {
    id: 'inventory-intelligence',
    path: '/inventory',
    label: 'Inventory Aging & Overstock',
    icon: 'inventory_2',
    group: 'supply',
    useCase: 'UC5',
    moduleRoute: 'inventory',
    summary: 'Aging, overstock risk and explainable risk scoring.',
    wireframed: true,
    capabilities: [
      'Inventory age and probability of crossing age thresholds',
      'Explainable additive risk score (Low / Medium / High / Critical)',
      'Overstock by configuration and location, value exposed',
      'Recommended actions with contributing factors',
    ],
  },
  {
    id: 'after-sales',
    path: '/after-sales',
    label: 'Sales ↔ Service Correlation',
    icon: 'handyman',
    group: 'after-sales',
    useCase: 'UC6',
    moduleRoute: 'after-sales',
    summary: 'Sales-to-service correlation and service-intensity index.',
    wireframed: false,
    capabilities: [
      'Correlate vehicle sales with service activity',
      'Service frequency by model, mileage band and time since sale',
      'Model-level service-intensity index',
      'Separate routine service from repair / warranty / recall',
    ],
  },
  {
    id: 'spare-parts',
    path: '/spare-parts',
    label: 'Spare Parts Prediction',
    icon: 'settings_suggest',
    group: 'after-sales',
    useCase: 'UC7',
    moduleRoute: 'spare-parts',
    summary: 'Intermittent spare-parts demand and stocking ranges.',
    wireframed: false,
    capabilities: [
      'Predict parts demand from vehicle population and sales mix',
      'Intermittent-demand methods (Croston / SBA / TSB)',
      'Supersession, alternates and model-to-part compatibility',
      'Stocking ranges with confidence and forecast range',
    ],
  },
  {
    id: 'decision-log',
    path: '/decisions',
    label: 'Decision Log',
    icon: 'gavel',
    group: 'governance',
    moduleRoute: 'decisions',
    summary: 'A governed audit trail of every recommendation and the human decision on it.',
    wireframed: true,
    capabilities: [
      'Every recommendation with what the engine advised, frozen at generation',
      'Who claimed it, what they decided, what they changed, and why',
      'Guard-validated transitions — no free status editing, no delete',
      'Approval chain, status-event timeline and realised outcome',
    ],
  },
  {
    id: 'lineage',
    path: '/lineage',
    label: 'Model & Data Lineage',
    icon: 'account_tree',
    group: 'governance',
    moduleRoute: 'models',
    summary: 'How data flows from Oracle Fusion to a decision, and what each metric is derived from.',
    wireframed: true,
    capabilities: [
      'Six-stage source-to-decision pipeline, read-only from Oracle Fusion',
      'Per-metric source and basis for every decision figure',
      'Each metric tagged confirmed or synthetic-demo, cross-checked against the platform',
      'CSV export of the metric provenance',
    ],
  },
  {
    id: 'platform-settings',
    path: '/settings',
    label: 'Settings',
    icon: 'settings',
    group: 'governance',
    moduleRoute: 'platform-admin',
    summary: "The platform's live risk configuration, shown read-only.",
    wireframed: true,
    capabilities: [
      'Risk-factor weights the engine renormalises by their sum',
      'Risk and aging bands with their thresholds and labels',
      'Analysis date, trailing-month horizon and cover ceiling',
      'Read-only transparency — configuration edits are governed separately',
    ],
  },
  {
    id: 'data-management',
    path: '/data',
    label: 'Data Health',
    icon: 'database',
    group: 'platform',
    moduleRoute: 'data-quality',
    summary: 'Which sources are real vs demo, and how clean the data behind every decision is.',
    wireframed: true,
    capabilities: [
      'Seven governed data sources with an honest real / demo / blocked status',
      'Data-quality score with its Healthy / Warning / Critical band',
      'Itemised data-quality checks with counts and severity',
      'CSV export of the governed data sources',
    ],
  },
];

export function navItemById(id: string): NavItem | undefined {
  return navItems.find((item) => item.id === id);
}

/** Items belonging to a group, in registry order. */
export function navItemsInGroup(group: NavGroupId): NavItem[] {
  return navItems.filter((item) => item.group === group);
}
