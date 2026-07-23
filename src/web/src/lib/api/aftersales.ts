import { useQuery } from '@tanstack/react-query';
import { apiGet, buildQuery } from './client';

export interface Provenance {
  provenance: string;
  note: string;
  generatedAtUtc: string;
}

export interface BandCount {
  band: string;
  events: number;
  eventsPerVehicle: number | null;
}

export interface ServiceTypeCount {
  serviceType: string;
  events: number;
  share: number;
}

export interface ServiceCoverage {
  vehiclesInOperation: number;
  vehiclesWithEvents: number;
  coverageRate: number | null;
  monthsOfHistory: number;
  reliabilityTier: string;
}

export interface ServiceCorrelation {
  lag0: number | null;
  best: number | null;
  bestLagMonths: number;
  interpretation: string;
}

export interface ModelServiceIntensity {
  model: string;
  totalEvents: number;
  vehiclesInOperation: number;
  eventsPerVehicle: number | null;
  intensityIndex: number | null;
  highIntensity: boolean;
  totalLaborHours: number;
  laborHoursPerVehicle: number | null;
  byMileageBand: BandCount[];
  byTimeSinceSale: BandCount[];
  byServiceType: ServiceTypeCount[];
  coverage: ServiceCoverage;
  correlation: ServiceCorrelation;
}

export interface ServiceIntensitySummary {
  modelsTracked: number;
  totalEvents: number;
  totalVehiclesInOperation: number;
  fleetEventsPerVehicle: number | null;
  averageIntensityIndex: number | null;
  highIntensityModels: number;
  overallCoverageRate: number | null;
  monthsOfHistory: number;
}

export interface AfterSalesSummaryResponse {
  summary: ServiceIntensitySummary;
  meta: Provenance;
}

export interface ModelIntensityRow {
  model: string;
  totalEvents: number;
  vehiclesInOperation: number;
  eventsPerVehicle: number | null;
  intensityIndex: number | null;
  highIntensity: boolean;
  totalLaborHours: number;
  laborHoursPerVehicle: number | null;
  coverageRate: number | null;
  monthsOfHistory: number;
  reliabilityTier: string;
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ByModelResponse {
  page: Paged<ModelIntensityRow>;
  meta: Provenance;
}

export interface ModelDetailResponse {
  model: ModelServiceIntensity;
  meta: Provenance;
}

export interface AfterSalesFilterOptions {
  models: string[];
  variants: string[];
  locations: string[];
  mileageBands: string[];
  serviceTypes: string[];
}

export interface AfterSalesQuery {
  page?: number;
  pageSize?: number;
  sort?: string;
  highOnly?: boolean;
  model?: string[];
}

const base = '/api/v1/after-sales';

export function useAfterSalesSummary() {
  return useQuery({
    queryKey: ['after-sales', 'summary'],
    queryFn: ({ signal }) => apiGet<AfterSalesSummaryResponse>(`${base}/service-intensity/summary`, signal),
    staleTime: 30_000,
  });
}

export function useAfterSalesByModel(query: AfterSalesQuery) {
  return useQuery({
    queryKey: ['after-sales', 'by-model', query],
    queryFn: ({ signal }) => apiGet<ByModelResponse>(`${base}/service-intensity/by-model${buildQuery({ ...query })}`, signal),
    staleTime: 30_000,
  });
}

export function useAfterSalesModel(model: string | null) {
  return useQuery({
    queryKey: ['after-sales', 'model', model],
    queryFn: ({ signal }) => apiGet<ModelDetailResponse>(`${base}/service-intensity/model/${encodeURIComponent(model!)}`, signal),
    enabled: model !== null,
    staleTime: 30_000,
  });
}

export function useAfterSalesFilterOptions() {
  return useQuery({
    queryKey: ['after-sales', 'filter-options'],
    queryFn: ({ signal }) => apiGet<AfterSalesFilterOptions>(`${base}/filter-options`, signal),
    staleTime: 300_000,
  });
}
