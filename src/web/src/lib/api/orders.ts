import { useQuery } from '@tanstack/react-query';
import { apiGet, buildQuery } from './client';

export interface OrderRow {
  model: string;
  variant: string;
  chosenModel: string;
  wmape: number | null;
  monthlyVelocity: number;
  forecastDemand: number;
  safetyStock: number;
  available: number;
  netRequirement: number;
  recommendedQuantity: number;
  overstockRisk: string;
  understockRisk: string;
  confidence: string;
  rationale: string;
}

export interface OrderResponse {
  scenario: Record<string, unknown>;
  items: OrderRow[];
  meta: { configurations: number; totalRecommendedUnits: number; generatedAtUtc: string };
}

export interface OrderScenarioQuery {
  horizon?: number;
  targetCoverMonths?: number;
  minOrderQuantity?: number;
  orderMultiple?: number;
  inbound?: number;
  confirmedOrders?: number;
  allocationLimit?: number;
  model?: string[];
}

export interface ProcurementRow {
  model: string;
  variant: string;
  demandMean: number;
  demandStd: number;
  leadTimeMonths: number;
  safetyStock: number;
  reorderPoint: number;
  orderUpToLevel: number;
  available: number;
  recommendedQuantity: number;
  rangeLow: number;
  rangeHigh: number;
  stockoutRisk: string;
  confidence: string;
  rationale: string;
}

export interface ProcurementResponse {
  scenario: Record<string, unknown>;
  items: ProcurementRow[];
  meta: { configurations: number; totalRecommendedUnits: number; generatedAtUtc: string };
}

export interface ProcurementScenarioQuery {
  serviceLevel?: number;
  leadTimeMonths?: number;
  reviewPeriodMonths?: number;
  minOrderQuantity?: number;
  orderMultiple?: number;
  inbound?: number;
  model?: string[];
}

export function useOrderOptimisation(query: OrderScenarioQuery) {
  return useQuery({
    queryKey: ['orders', 'order-optimisation', query],
    queryFn: ({ signal }) =>
      apiGet<OrderResponse>(`/api/v1/recommendations/order-optimisation${buildQuery({ ...query })}`, signal),
    staleTime: 30_000,
  });
}

export function useProcurement(query: ProcurementScenarioQuery) {
  return useQuery({
    queryKey: ['procurement', 'recommendations', query],
    queryFn: ({ signal }) =>
      apiGet<ProcurementResponse>(`/api/v1/procurement/recommendations${buildQuery({ ...query })}`, signal),
    staleTime: 30_000,
  });
}
