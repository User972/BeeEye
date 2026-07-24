import { useQuery } from '@tanstack/react-query';
import { apiGet } from './client';

/** One stage of the source-to-decision pipeline (V3-GOV-009). */
export interface PipelineStage {
  title: string;
  description: string;
  icon: string;
  kind: string;
}

export type LineageState = 'confirmed' | 'demo';

/** One decision metric with its source, basis and confirmed/demo provenance. */
export interface LineageMetric {
  metric: string;
  source: string;
  basis: string;
  state: LineageState;
}

export interface LineageResponse {
  pipeline: PipelineStage[];
  metrics: LineageMetric[];
}

export const lineageKeys = {
  lineage: ['models', 'lineage'] as const,
};

/** V3-GOV-009 — the source-to-decision pipeline and per-metric provenance behind the Lineage screen. */
export function useLineage() {
  return useQuery({
    queryKey: lineageKeys.lineage,
    queryFn: ({ signal }) => apiGet<LineageResponse>('/api/v1/models/lineage', signal),
    staleTime: 60_000,
  });
}
