import { useQuery } from '@tanstack/react-query';
import { apiGet } from './client';

/** How a source's status renders: word + icon + colour, never colour alone. */
export type DataSourceKind = 'ready' | 'assumptions' | 'demo' | 'blocked';

/** One governed data source on the Data Health screen (V3-GOV-008). */
export interface DataSource {
  name: string;
  system: string;
  status: string;
  statusKind: DataSourceKind;
  /** A display string, so a synthetic count is never presented as a measured one. */
  rows: string;
  coverage: string;
  note: string;
}

/** One data-quality check. */
export interface DataQualityIssue {
  id: string;
  label: string;
  count: number;
  severity: string;
  note: string;
}

export interface DataHealthResponse {
  score: number;
  scoreBand: string;
  salesRows: number;
  invRows: number;
  coverage: string;
  models: number;
  locations: number;
  sources: DataSource[];
  issues: DataQualityIssue[];
  generatedAtUtc: string;
}

export const dataHealthKeys = {
  health: ['data-quality', 'health'] as const,
};

/** V3-GOV-008 — the governed data sources, data-quality score and issues behind the Data Health screen. */
export function useDataHealth() {
  return useQuery({
    queryKey: dataHealthKeys.health,
    queryFn: ({ signal }) => apiGet<DataHealthResponse>('/api/v1/data-quality/health', signal),
    staleTime: 60_000,
  });
}
