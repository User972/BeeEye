import { useRef } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { UseMutationResult } from '@tanstack/react-query';
import { ApiError, apiGet, apiPost, buildQuery, newIdempotencyKey } from './client';
import type { AiLabelKind } from '@/components/ui/aiLabels';

// ---------------------------------------------------------------------------
// Types — mirroring the DTOs in BeeEye.Modules.Predictions.Contracts.
// ---------------------------------------------------------------------------

/** The subject kinds the eight providers claim. One provider per kind, enforced at server start-up. */
export type SubjectKind =
  | 'order-configuration'
  | 'forecast-scope'
  | 'configuration'
  | 'procurement-item'
  | 'inventory-unit'
  | 'service-model'
  | 'part'
  | 'decision'
  | 'brief';

export type ImpactTone = 'neutral' | 'positive' | 'negative' | 'warning';
export type LineageKind = 'fusion' | 'workbook' | 'demo' | 'derived';
export type ConfidenceBandName = 'Low' | 'Medium' | 'High';

export interface ImpactTile {
  label: string;
  /** Pre-formatted on the server with the invariant culture. Never re-formatted here. */
  value: string;
  tone: ImpactTone;
}

export interface ConfidenceStatement {
  band: ConfidenceBandName;
  percent: number | null;
  why: string[];
}

export interface Driver {
  label: string;
  detail: string | null;
}

export interface EvidencePoint {
  label: string;
  value: number;
  comparison: number | null;
}

export interface EvidenceSeries {
  period: string;
  points: EvidencePoint[];
  note: string | null;
  valueLabel: string;
  comparisonLabel: string | null;
}

export interface LineageNode {
  label: string;
  kind: LineageKind;
}

export interface ModelInfo {
  name: string;
  version: string;
  recalculated: string;
  horizon: string;
  validation: string;
  error: string;
}

export interface Ownership {
  ownerRole: string;
  status: string;
}

export interface Explanation {
  title: string;
  module: string;
  /** One of the eight `LABELS` keys. Always present. */
  label: AiLabelKind;
  recommendation: string | null;
  impacts: ImpactTile[];
  /** **Null when the engine computed no band.** Never defaulted to "Medium". */
  confidence: ConfidenceStatement | null;
  drivers: Driver[];
  evidence: EvidenceSeries | null;
  assumptions: string[];
  lineage: LineageNode[];
  model: ModelInfo | null;
  ownership: Ownership | null;
  isDemoData: boolean;
}

/** A context that could not be reached. Rendered, never swallowed. */
export interface ExplanationGap {
  area: string;
  reason: string;
}

export interface FeedbackEntry {
  verdict: FeedbackVerdict;
  note: string | null;
  submittedBy: string;
  submittedAtUtc: string;
}

export interface ExplanationResponse {
  subjectKind: string;
  subjectRef: string;
  /** Null only when every claimant failed — see `gaps`. */
  explanation: Explanation | null;
  gaps: ExplanationGap[];
  feedback: FeedbackEntry[];
  feedbackCaveat: string;
  generatedAtUtc: string;
}

export type FeedbackVerdict = 'Useful' | 'NeedsReview' | 'Incorrect' | 'MissingContext';

export interface FeedbackResponse {
  subjectKind: string;
  subjectRef: string;
  verdict: FeedbackVerdict;
  submittedAtUtc: string;
  caveat: string;
}

// ---------------------------------------------------------------------------
// Display vocabulary
// ---------------------------------------------------------------------------

/** v3's four verdicts, with its icons. */
export const feedbackVerdicts: { value: FeedbackVerdict; label: string; icon: string }[] = [
  { value: 'Useful', label: 'Useful', icon: 'thumb_up' },
  { value: 'NeedsReview', label: 'Needs review', icon: 'flag' },
  { value: 'Incorrect', label: 'Incorrect', icon: 'close' },
  { value: 'MissingContext', label: 'Missing context', icon: 'help' },
];

/** Lineage chip icon per kind. Colour lives in `components.css` as `.ex__chip--{kind}`. */
export const lineageIcons: Record<LineageKind, string> = {
  fusion: 'cloud',
  workbook: 'table_view',
  demo: 'biotech',
  derived: 'calculate',
};

// ---------------------------------------------------------------------------
// Queries
// ---------------------------------------------------------------------------

export interface ExplainSubject {
  kind: SubjectKind;
  /** The subject's identifier, in whatever form the owning context uses. */
  ref: string;
}

export const explainKeys = {
  all: ['explainability'] as const,
  subject: (subject: ExplainSubject) => ['explainability', subject.kind, subject.ref] as const,
};

/**
 * Fetches an explanation.
 *
 * `enabled` is driven by the subject, so the drawer costs nothing until it is opened — and, once
 * opened, it opens *immediately* and fills in, rather than appearing to hang while the request runs.
 */
export function useExplanation(subject: ExplainSubject | null) {
  return useQuery({
    queryKey: explainKeys.subject(subject ?? { kind: 'brief', ref: '' }),
    queryFn: ({ signal }) =>
      apiGet<ExplanationResponse>(
        `/api/v1/predictions/explain${buildQuery({ kind: subject!.kind, ref: subject!.ref })}`,
        signal,
      ),
    enabled: subject !== null,
    staleTime: 30_000,
    // A 403 or a 404 answers the same way every time; retrying only delays showing the user why.
    retry: (failureCount, error) =>
      !(error instanceof ApiError && error.status < 500) && failureCount < 1,
  });
}

// ---------------------------------------------------------------------------
// Mutations
// ---------------------------------------------------------------------------

export interface SubmitFeedbackVariables {
  kind: SubjectKind;
  ref: string;
  verdict: FeedbackVerdict;
  note: string | null;
}

/**
 * Records a verdict on an explanation.
 *
 * **No optimistic update**, for the same reason S6 recorded: the server may refuse (an unknown
 * verdict, a replayed key carrying a different body, a missing permission), and showing a state the
 * server rejected is exactly the failure mode this platform's write path exists to prevent.
 *
 * One idempotency key per intent, held in a ref so it survives TanStack Query's retry — a fresh key
 * per retry would defeat the whole mechanism and append the same verdict twice.
 */
export function useSubmitFeedback(): UseMutationResult<
  FeedbackResponse,
  unknown,
  SubmitFeedbackVariables
> {
  const queryClient = useQueryClient();
  const keys = useRef(new Map<SubmitFeedbackVariables, string>());

  return useMutation({
    mutationFn: (variables: SubmitFeedbackVariables) => {
      let key = keys.current.get(variables);
      if (key === undefined) {
        key = newIdempotencyKey();
        keys.current.set(variables, key);
      }

      return apiPost<FeedbackResponse>(
        '/api/v1/predictions/explain/feedback',
        {
          kind: variables.kind,
          ref: variables.ref,
          verdict: variables.verdict,
          note: variables.note,
        },
        { idempotencyKey: key },
      );
    },
    retry: (failureCount, error) =>
      !(error instanceof ApiError && error.status < 500) && failureCount < 1,
    onSettled: (_data, _error, variables) => {
      keys.current.delete(variables);

      // Refetched on success *and* failure, so what the drawer shows is what the server holds.
      void queryClient.invalidateQueries({
        queryKey: explainKeys.subject({ kind: variables.kind, ref: variables.ref }),
      });
    },
  });
}
