import { useQuery } from '@tanstack/react-query';
import { apiGet, buildQuery } from './client';

export interface BandValue {
  key: string;
  units: number;
  value: number;
}

export interface DimensionValue {
  key: string;
  units: number;
  value: number;
  accumulatedHoldingCost: number;
}

export interface InventorySummary {
  count: number;
  value: number;
  accumulatedHoldingCost: number;
  dailyHoldingCost: number;
  averageInventoryAge: number;
  averageManufacturingAge: number;
  averageLeadTime: number;
  highRiskValue: number;
  criticalValue: number;
  criticalCount: number;
  highCount: number;
  decliningValue: number;
  transferCount: number;
  promotionCount: number;
  discountCount: number;
  pauseCount: number;
  byRisk: BandValue[];
  byAging: BandValue[];
  byManufacturing: BandValue[];
  byLocation: DimensionValue[];
  byModel: DimensionValue[];
  byVariant: DimensionValue[];
  byBrand: DimensionValue[];
  byColour: DimensionValue[];
  byInterior: DimensionValue[];
}

export interface InventoryMeta {
  analysisDate: string;
  totalUnits: number;
  filteredUnits: number;
  generatedAtUtc: string;
}

export interface InventorySummaryResponse {
  summary: InventorySummary;
  meta: InventoryMeta;
}

export interface InventoryItemRow {
  stockId: string;
  brand: string;
  model: string;
  variant: string;
  colour: string;
  location: string;
  inventoryAgeDays: number;
  agingBand: string;
  velocity: number;
  stockCover: number;
  trendDirection: string;
  riskScore: number;
  riskBand: string;
  purchasePrice: number;
  accumulatedHoldingCost: number;
  recommendedAction: string;
}

export interface InventoryItemsResponse {
  items: InventoryItemRow[];
  page: number;
  pageSize: number;
  totalCount: number;
  meta: InventoryMeta;
}

export interface RiskFactor {
  key: string;
  label: string;
  points: number;
  detail: string;
}

export interface InventoryRecommendation {
  action: string;
  confidence: string;
  why: string;
  evidence: string[];
  outcome: string;
  assumptions: string[];
  destination?: string | null;
  discountPct?: number | null;
}

// The detail endpoint returns the full risk record, which exposes `recommendation` (an object)
// but not the flattened `recommendedAction` scalar the grid rows carry — so omit it here.
export interface InventoryItemDetail extends Omit<InventoryItemRow, 'recommendedAction'> {
  chassisNo: string;
  interior: string;
  type: string;
  dateOfPurchase: string;
  dateOfManufacture: string;
  serviceDate: string | null;
  holdingCostPerDay: number;
  leadTimeDays: number;
  manufacturingAgeDays: number;
  manufacturingBand: string;
  demandBasis: string;
  demandConfidence: string;
  demandDetail: string;
  groupStock: number;
  trendChangePct: number;
  factors: RiskFactor[];
  recommendation: InventoryRecommendation;
}

export interface InventoryFilterOptions {
  brands: string[];
  models: string[];
  variants: string[];
  types: string[];
  locations: string[];
  colours: string[];
  interiors: string[];
  riskBands: string[];
}

export interface InventoryQuery {
  model?: string[];
  variant?: string[];
  location?: string[];
  riskBand?: string[];
  analysisDate?: string;
  sort?: string;
  page?: number;
  pageSize?: number;
}

const base = '/api/v1/inventory';

export function useInventorySummary(query: InventoryQuery) {
  return useQuery({
    queryKey: ['inventory', 'summary', query],
    queryFn: ({ signal }) =>
      apiGet<InventorySummaryResponse>(`${base}/summary${buildQuery({ ...query })}`, signal),
    staleTime: 30_000,
  });
}

export function useInventoryItems(query: InventoryQuery) {
  return useQuery({
    queryKey: ['inventory', 'items', query],
    queryFn: ({ signal }) =>
      apiGet<InventoryItemsResponse>(`${base}/items${buildQuery({ ...query })}`, signal),
    staleTime: 30_000,
  });
}

export function useInventoryItem(stockId: string | null) {
  return useQuery({
    queryKey: ['inventory', 'item', stockId],
    enabled: stockId !== null,
    queryFn: ({ signal }) => apiGet<InventoryItemDetail>(`${base}/items/${stockId}`, signal),
  });
}

export function useInventoryFilterOptions() {
  return useQuery({
    queryKey: ['inventory', 'filter-options'],
    queryFn: ({ signal }) => apiGet<InventoryFilterOptions>(`${base}/filter-options`, signal),
    staleTime: 300_000,
  });
}
