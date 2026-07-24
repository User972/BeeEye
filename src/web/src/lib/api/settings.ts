import { useQuery } from '@tanstack/react-query';
import { apiGet } from './client';

/** The five risk-factor weights and their sum (V3-GOV-010). */
export interface RiskWeights {
  cover: number;
  aging: number;
  demand: number;
  holding: number;
  lead: number;
  sum: number;
  note: string;
}

/** One labelled band. `threshold` is the inclusive upper bound, null for the open-ended top band. */
export interface SettingsBand {
  label: string;
  threshold: number | null;
  range: string;
}

export interface SettingsResponse {
  weights: RiskWeights;
  riskBands: SettingsBand[];
  agingBands: SettingsBand[];
  analysisDate: string;
  trailingMonths: number;
  coverMax: number;
  note: string;
}

export const settingsKeys = {
  settings: ['platform-admin', 'settings'] as const,
};

/** V3-GOV-010 — the platform's real risk configuration, read-only, behind the Settings screen. */
export function useSettings() {
  return useQuery({
    queryKey: settingsKeys.settings,
    queryFn: ({ signal }) => apiGet<SettingsResponse>('/api/v1/platform-admin/settings', signal),
    staleTime: 60_000,
  });
}
