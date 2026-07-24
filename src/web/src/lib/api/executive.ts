import { useQuery } from '@tanstack/react-query';
import { apiGet } from './client';

/** One ranked driver behind a decision's priority. */
export interface DecisionFactor {
  name: string;
  percent: number;
}

export type DecisionSeverity = 'Low' | 'Medium' | 'High';
export type DecisionKind = 'Risk' | 'Opportunity';
export type ConfidenceBand = 'Low' | 'Medium' | 'High';

/** A material exception raised by one of the intelligence modules (UC8). */
export interface Decision {
  id: string;
  title: string;
  area: string;
  /** Nav item id to drill into — matches `config/navigation.ts`. */
  screen: string;
  severity: DecisionSeverity;
  impactSar: number;
  kind: DecisionKind;
  /** 0–100 multiplicative priority. */
  priority: number;
  confidence: ConfidenceBand;
  confidencePct: number;
  dueDays: number;
  whyNow: string;
  action: string;
  evidence: string;
  ownerRole: string;
  /** True when the decision derives from synthetic demo data and must be labelled. */
  isDemo: boolean;
  factors: DecisionFactor[];
}

export interface DecisionFeedSummary {
  total: number;
  critical: number;
  lowConfidence: number;
  dueThisWeek: number;
  opportunityValueSar: number;
  riskValueSar: number;
  demoDataCount: number;
}

/** A context that could not be assessed. Surfaced so an incomplete feed is never mistaken for a quiet one. */
export interface DecisionFeedGap {
  area: string;
  reason: string;
}

export interface DecisionFeedResponse {
  decisions: Decision[];
  summary: DecisionFeedSummary;
  narrative: string;
  gaps: DecisionFeedGap[];
  generatedAtUtc: string;
}

export const executiveKeys = {
  decisionFeed: ['executive-insights', 'decision-feed'] as const,
};

/** UC8 — the ranked cross-module decision feed behind the Decision Cockpit. */
export function useDecisionFeed() {
  return useQuery({
    queryKey: executiveKeys.decisionFeed,
    queryFn: ({ signal }) =>
      apiGet<DecisionFeedResponse>('/api/v1/executive-insights/decision-feed', signal),
    staleTime: 60_000,
  });
}
