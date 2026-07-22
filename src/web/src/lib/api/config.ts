import { useQuery } from '@tanstack/react-query';
import { apiGet, buildQuery } from './client';

export interface RotationBand {
  key: string;
  configurations: number;
  units: number;
}

export interface ConfigDemandSummary {
  configurations: number;
  totalUnits: number;
  fastCount: number;
  mediumCount: number;
  slowCount: number;
  deadCount: number;
  decayAlerts: number;
  coldStart: number;
  stockoutSuspected: number;
  byRotation: RotationBand[];
}

export interface ConfigMeta {
  totalConfigurations: number;
  filteredConfigurations: number;
  generatedAtUtc: string;
}

export interface ConfigSummaryResponse {
  summary: ConfigDemandSummary;
  meta: ConfigMeta;
}

export interface ConfigRow {
  model: string;
  variant: string;
  colour: string;
  interior: string;
  totalUnits: number;
  recentVelocity: number;
  decayPct: number;
  trendDirection: string;
  rotationClass: string;
  currentStock: number;
  topRegionShare: number;
  decayAlert: boolean;
  stockoutSuspected: boolean;
  isColdStart: boolean;
}

export interface ConfigListResponse {
  items: ConfigRow[];
  page: number;
  pageSize: number;
  totalCount: number;
  meta: ConfigMeta;
}

export interface ConfigFilterOptions {
  models: string[];
  variants: string[];
  colours: string[];
  interiors: string[];
  rotations: string[];
}

export interface ConfigQuery {
  model?: string[];
  variant?: string[];
  colour?: string[];
  rotation?: string[];
  sort?: string;
  page?: number;
  pageSize?: number;
}

const base = '/api/v1/sales-actuals/config-demand';

export function useConfigSummary(query: ConfigQuery) {
  return useQuery({
    queryKey: ['config', 'summary', query],
    queryFn: ({ signal }) => apiGet<ConfigSummaryResponse>(`${base}/summary${buildQuery({ ...query })}`, signal),
    staleTime: 30_000,
  });
}

export function useConfigs(query: ConfigQuery) {
  return useQuery({
    queryKey: ['config', 'configs', query],
    queryFn: ({ signal }) => apiGet<ConfigListResponse>(`${base}/configs${buildQuery({ ...query })}`, signal),
    staleTime: 30_000,
  });
}

export function useConfigFilterOptions() {
  return useQuery({
    queryKey: ['config', 'filter-options'],
    queryFn: ({ signal }) => apiGet<ConfigFilterOptions>(`${base}/filter-options`, signal),
    staleTime: 300_000,
  });
}
