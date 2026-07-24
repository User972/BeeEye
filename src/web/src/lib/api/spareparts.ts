import { useQuery } from '@tanstack/react-query';
import { apiGet, buildQuery } from './client';
import type { Paged, Provenance } from './aftersales';

export interface SparePartsScenario {
  serviceLevel: number;
  reviewPeriodMonths: number;
}

export interface DemandClassCount {
  demandClass: string;
  count: number;
}

export interface SparePartsSummary {
  distinctParts: number;
  stockingPoints: number;
  lowDataPoints: number;
  atRiskPoints: number;
  predictedMonthlyDemandTotal: number;
  byDemandClass: DemandClassCount[];
}

export interface SparePartsSummaryResponse {
  scenario: SparePartsScenario;
  summary: SparePartsSummary;
  meta: Provenance;
}

export interface PartDemandRow {
  partNumber: string;
  name: string;
  category: string;
  location: string;
  demandClass: string;
  method: string;
  predictedMonthlyDemand: number | null;
  stockingRangeLow: number | null;
  stockingRangeHigh: number | null;
  currentStock: number;
  inboundStock: number;
  leadTimeDays: number;
  reorderPoint: number | null;
  stockoutRisk: string;
  holdingRisk: string;
  confidence: string;
  insufficientData: boolean;
}

export interface PartsDemandResponse {
  scenario: SparePartsScenario;
  page: Paged<PartDemandRow>;
  meta: Provenance;
}

export interface MethodComparison {
  ses: number;
  croston: number;
  sba: number;
  tsb: number;
}

export interface PartRecommendation {
  partNumber: string;
  name: string;
  category: string;
  class: string;
  method: string;
  adi: number;
  cv2: number;
  nonZeroPeriods: number;
  periods: number;
  predictedMonthlyDemand: number | null;
  monthlyRangeLow: number | null;
  monthlyRangeHigh: number | null;
  leadTimeMonths: number;
  leadTimeDemand: number | null;
  safetyStock: number | null;
  reorderPoint: number | null;
  orderUpToLevel: number | null;
  available: number;
  recommendedQuantity: number | null;
  stockingRangeLow: number | null;
  stockingRangeHigh: number | null;
  stockoutRisk: string;
  holdingRisk: string;
  confidence: string;
  insufficientData: boolean;
  action: string;
  rationale: string;
  comparison: MethodComparison;
}

export interface UsagePoint {
  month: string;
  quantity: number;
}

export interface SupersessionInfo {
  oldPartNumber: string;
  newPartNumber: string;
  effectiveDate: string;
}

export interface PartDetailResponse {
  scenario: SparePartsScenario;
  national: PartRecommendation;
  usageHistory: UsagePoint[];
  byLocation: PartDemandRow[];
  compatibleModels: string[];
  rolledUpSupersessions: SupersessionInfo[];
  supersededByPartNumber: string | null;
  meta: Provenance;
}

export interface SparePartsFilterOptions {
  categories: string[];
  models: string[];
  demandClasses: string[];
}

export interface SparePartsScenarioQuery {
  serviceLevel?: number;
  reviewPeriodMonths?: number;
}

export interface SparePartsListQuery extends SparePartsScenarioQuery {
  page?: number;
  pageSize?: number;
  sort?: string;
  category?: string[];
  model?: string[];
  lowDataOnly?: boolean;
  atRiskOnly?: boolean;
}

const base = '/api/v1/spare-parts';

export function useSparePartsSummary(query: SparePartsScenarioQuery) {
  return useQuery({
    queryKey: ['spare-parts', 'summary', query],
    queryFn: ({ signal }) => apiGet<SparePartsSummaryResponse>(`${base}/demand/summary${buildQuery({ ...query })}`, signal),
    staleTime: 30_000,
  });
}

export function useSpareParts(query: SparePartsListQuery) {
  return useQuery({
    queryKey: ['spare-parts', 'parts', query],
    queryFn: ({ signal }) => apiGet<PartsDemandResponse>(`${base}/demand/parts${buildQuery({ ...query })}`, signal),
    staleTime: 30_000,
  });
}

export function useSparePart(partNumber: string | null, scenario: SparePartsScenarioQuery) {
  return useQuery({
    queryKey: ['spare-parts', 'part', partNumber, scenario],
    queryFn: ({ signal }) =>
      apiGet<PartDetailResponse>(`${base}/demand/part/${encodeURIComponent(partNumber!)}${buildQuery({ ...scenario })}`, signal),
    enabled: partNumber !== null,
    staleTime: 30_000,
  });
}

export function useSparePartsFilterOptions() {
  return useQuery({
    queryKey: ['spare-parts', 'filter-options'],
    queryFn: ({ signal }) => apiGet<SparePartsFilterOptions>(`${base}/filter-options`, signal),
    staleTime: 300_000,
  });
}
