/**
 * Single source of truth for the application's screens. Drives the navigation
 * rail, the router, and each use-case page's scaffold content. Screens and the
 * visual language derive from the Meridian BI wireframe
 * (docs/architecture/wireframe-analysis/screen-inventory.md); the missing
 * use-case workflows are specified under docs/product/use-cases/.
 */

export type NavSection = 'overview' | 'intelligence' | 'platform';

export interface NavItem {
  /** Stable id; matches the lazy page module file name under src/pages. */
  id: string;
  path: string;
  label: string;
  /** Material Symbols Outlined icon name. */
  icon: string;
  section: NavSection;
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

export const navItems: NavItem[] = [
  {
    id: 'executive-cockpit',
    path: '/',
    label: 'Executive Cockpit',
    icon: 'dashboard',
    section: 'overview',
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
    id: 'sales-forecasting',
    path: '/forecasting',
    label: 'Sales Forecasting',
    icon: 'insights',
    section: 'intelligence',
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
    id: 'inventory-intelligence',
    path: '/inventory',
    label: 'Inventory Intelligence',
    icon: 'inventory_2',
    section: 'intelligence',
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
    id: 'order-optimisation',
    path: '/order-optimisation',
    label: 'Order Optimisation',
    icon: 'tune',
    section: 'intelligence',
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
    id: 'configuration-demand',
    path: '/configuration-demand',
    label: 'Configuration Demand',
    icon: 'grid_view',
    section: 'intelligence',
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
    label: 'Procurement',
    icon: 'local_shipping',
    section: 'intelligence',
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
    id: 'after-sales',
    path: '/after-sales',
    label: 'After-Sales Correlation',
    icon: 'build',
    section: 'intelligence',
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
    label: 'Spare Parts',
    icon: 'settings',
    section: 'intelligence',
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
    id: 'data-management',
    path: '/data',
    label: 'Data Management',
    icon: 'database',
    section: 'platform',
    moduleRoute: 'data-quality',
    summary: 'Ingestion runs, data-quality rules and issue resolution.',
    wireframed: true,
    capabilities: [
      'Oracle Fusion ingestion runs and reconciliation counts',
      'Data-quality rules across nine categories',
      'Critical-quality gates that block model runs',
      'Issue triage, ownership and override policy',
    ],
  },
  {
    id: 'platform-settings',
    path: '/settings',
    label: 'Platform & Settings',
    icon: 'admin_panel_settings',
    section: 'platform',
    moduleRoute: 'platform-admin',
    summary: 'Feature flags, licensing, thresholds and configuration.',
    wireframed: true,
    capabilities: [
      'Configurable thresholds by business unit or category',
      'Feature flags and module entitlement',
      'Licensing status and grace-period handling',
      'Analysis-date and model-version configuration',
    ],
  },
];

export const navSections: { id: NavSection; label: string }[] = [
  { id: 'overview', label: 'Overview' },
  { id: 'intelligence', label: 'Intelligence Modules' },
  { id: 'platform', label: 'Platform' },
];

export function navItemById(id: string): NavItem | undefined {
  return navItems.find((item) => item.id === id);
}
