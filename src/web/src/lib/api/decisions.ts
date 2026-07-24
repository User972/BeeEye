import { useRef } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { UseMutationResult } from '@tanstack/react-query';
import { ApiError, apiGet, apiPost, buildQuery, newIdempotencyKey } from './client';

// ---------------------------------------------------------------------------
// Types — mirroring the DTOs in BeeEye.Modules.DecisionsAndOutcomes.Contracts.
// ---------------------------------------------------------------------------

/** ADR-0006's nine lifecycle states. v3's `Assigned` and `Snoozed` have no counterpart. */
export type RecommendationStatus =
  | 'Generated'
  | 'UnderReview'
  | 'Accepted'
  | 'AcceptedModified'
  | 'Rejected'
  | 'Expired'
  | 'Superseded'
  | 'Implemented'
  | 'OutcomeRecorded';

export type DecisionOutcome = 'Open' | 'Accepted' | 'AcceptedModified' | 'Rejected';

/** The transitions the server will accept, as it reports them per row. */
export type DecisionAction =
  | 'claim'
  | 'accept'
  | 'accept-with-modification'
  | 'reject'
  | 'sign-off'
  | 'implemented'
  | 'record-outcome';

export interface Modification {
  field: string;
  from: number;
  to: number;
  rationale: string | null;
}

export interface ApprovalStep {
  stepNumber: number;
  approverRole: string;
  status: 'Pending' | 'Approved' | 'Declined';
  actedBy: string | null;
  actedAtUtc: string | null;
  note: string | null;
}

export interface ActionOutcome {
  metric: string;
  realisedValue: number;
  unit: string | null;
  measuredAtUtc: string;
  recordedBy: string;
  note: string | null;
}

export interface StatusEvent {
  fromStatus: RecommendationStatus | null;
  toStatus: RecommendationStatus;
  actor: string;
  reason: string | null;
  atUtc: string;
}

export interface DecisionSummary {
  id: string;
  outcome: DecisionOutcome;
  openedBy: string;
  openedAtUtc: string;
  decidedBy: string | null;
  decidedAtUtc: string | null;
  note: string | null;
  modification: Modification | null;
  implementedBy: string | null;
  implementedAtUtc: string | null;
}

/** One row of the log. Its identity is the recommendation; `decisionId` appears once claimed. */
export interface DecisionLogItem {
  recommendationId: string;
  decisionId: string | null;
  ruleId: string;
  subjectRef: string;
  area: string;
  action: string;
  evidence: string;
  status: RecommendationStatus;
  outcome: DecisionOutcome | null;
  impactSar: number;
  priority: number;
  ownerRole: string;
  isDemoData: boolean;
  source: string;
  createdAtUtc: string;
  decidedBy: string | null;
  decidedAtUtc: string | null;
  modification: Modification | null;
  availableActions: DecisionAction[];
}

export interface DecisionLogPage {
  items: DecisionLogItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  /** One entry per status, honouring every filter except status itself. */
  statusCounts: Record<string, number>;
}

export interface RecommendationSnapshot {
  id: string;
  ruleId: string;
  subjectRef: string;
  area: string;
  action: string;
  rationale: string;
  evidence: string[];
  expectedOutcome: string;
  confidence: string;
  assumptions: string[];
  impactSar: number;
  priority: number;
  ownerRole: string;
  isDemoData: boolean;
  rulesetVersion: string;
  datasetVersion: string;
  analysisDate: string;
  currentStatus: RecommendationStatus;
  validUntilUtc: string | null;
  supersededByRecommendationId: string | null;
  createdAtUtc: string;
}

export interface DecisionDetail {
  recommendation: RecommendationSnapshot;
  decision: DecisionSummary | null;
  approvalSteps: ApprovalStep[];
  statusEvents: StatusEvent[];
  outcome: ActionOutcome | null;
  availableActions: DecisionAction[];
}

export interface TransitionResponse {
  recommendationId: string;
  decisionId: string;
  status: RecommendationStatus;
  outcome: DecisionOutcome;
  message: string;
}

export interface DecisionLogFilters {
  status?: RecommendationStatus | null;
  area?: string | null;
  outcome?: DecisionOutcome | null;
  q?: string | null;
  page?: number;
  pageSize?: number;
}

// ---------------------------------------------------------------------------
// Display vocabulary — v3's labels and colours over ADR-0006's states.
// ---------------------------------------------------------------------------

/**
 * v3 label per ADR-0006 state (V3-CONFLICT-2). `Accepted` and `AcceptedModified` both render
 * under v3's "Accepted" chip wording, distinguished in words rather than by colour alone.
 */
export const statusLabels: Record<RecommendationStatus, string> = {
  Generated: 'New',
  UnderReview: 'Under review',
  Accepted: 'Accepted',
  AcceptedModified: 'Accepted with modification',
  Implemented: 'In progress',
  OutcomeRecorded: 'Completed',
  Rejected: 'Rejected',
  Expired: 'Expired',
  Superseded: 'Superseded',
};

/** v3's status colour tokens, carried over verbatim. Colour never carries meaning alone. */
export const statusColours: Record<RecommendationStatus, string> = {
  Generated: 'var(--primary-2)',
  UnderReview: 'var(--warn)',
  Accepted: 'var(--risk-low)',
  AcceptedModified: 'var(--risk-low)',
  Implemented: 'var(--primary)',
  OutcomeRecorded: 'var(--risk-low)',
  Rejected: 'var(--risk-crit)',
  Expired: 'var(--text-muted)',
  Superseded: 'var(--text-faint)',
};

/** Chip order, following the lifecycle rather than the alphabet. */
export const statusOrder: RecommendationStatus[] = [
  'Generated',
  'UnderReview',
  'Accepted',
  'AcceptedModified',
  'Implemented',
  'OutcomeRecorded',
  'Rejected',
  'Expired',
  'Superseded',
];

/** Button wording per action. */
export const actionLabels: Record<DecisionAction, string> = {
  claim: 'Claim for review',
  accept: 'Accept',
  'accept-with-modification': 'Accept with modification',
  reject: 'Reject',
  'sign-off': 'Sign off',
  implemented: 'Mark implemented',
  'record-outcome': 'Record outcome',
};

/** The values a human may change, and the bounds the server enforces on each. */
export const modifiableFields = [
  { value: 'proposed_qty', label: 'Proposed quantity' },
  { value: 'discount_pct', label: 'Discount %' },
  { value: 'transfer_qty', label: 'Transfer quantity' },
  { value: 'procurement_qty', label: 'Procurement quantity' },
] as const;

// ---------------------------------------------------------------------------
// Queries
// ---------------------------------------------------------------------------

export const decisionKeys = {
  all: ['decisions'] as const,
  log: (filters: DecisionLogFilters) => ['decisions', 'log', filters] as const,
  detail: (id: string) => ['decisions', 'detail', id] as const,
};

export function useDecisionLog(filters: DecisionLogFilters) {
  return useQuery({
    queryKey: decisionKeys.log(filters),
    queryFn: ({ signal }) =>
      apiGet<DecisionLogPage>(
        `/api/v1/decisions${buildQuery({
          status: filters.status ?? undefined,
          area: filters.area ?? undefined,
          outcome: filters.outcome ?? undefined,
          q: filters.q ?? undefined,
          page: filters.page,
          pageSize: filters.pageSize,
        })}`,
        signal,
      ),
    staleTime: 30_000,
  });
}

export function useDecisionDetail(recommendationId: string | null) {
  return useQuery({
    queryKey: decisionKeys.detail(recommendationId ?? ''),
    queryFn: ({ signal }) =>
      apiGet<DecisionDetail>(`/api/v1/decisions/${recommendationId}`, signal),
    enabled: recommendationId !== null,
  });
}

// ---------------------------------------------------------------------------
// Mutations
// ---------------------------------------------------------------------------

/**
 * Shared shape for every decision mutation.
 *
 * Two properties matter here:
 *
 * 1. **One idempotency key per intent.** The key is minted when the mutation function runs and
 *    held in a ref that survives TanStack Query's retries, so a retried request is recognised as
 *    the same intent rather than recorded as a second decision.
 * 2. **No optimistic update.** A guard may refuse the transition server-side, and showing a state
 *    the server rejected is precisely the failure mode ADR-0006 exists to prevent. The row waits
 *    for the answer.
 */
function useDecisionMutation<TVariables>(
  send: (variables: TVariables, idempotencyKey: string) => Promise<TransitionResponse>,
): UseMutationResult<TransitionResponse, unknown, TVariables> {
  const queryClient = useQueryClient();

  // A ref, not a plain local: TanStack re-reads these options on every render, so a Map created
  // during render would be replaced mid-flight and a retry would mint a second key for one intent.
  const keys = useRef(new Map<TVariables, string>());

  return useMutation({
    mutationFn: (variables: TVariables) => {
      let key = keys.current.get(variables);
      if (key === undefined) {
        key = newIdempotencyKey();
        keys.current.set(variables, key);
      }
      return send(variables, key);
    },
    // A refusal is not worth retrying — a guard, a bound or a conflict answers the same way every
    // time, and retrying only delays showing the user why. A transport failure is worth one retry,
    // and the reused idempotency key is what makes that retry safe rather than a second decision.
    retry: (failureCount, error) =>
      !(error instanceof ApiError && error.status < 500) && failureCount < 1,
    onSettled: (data, _error, variables) => {
      // The intent has resolved one way or the other, so its key is no longer needed.
      keys.current.delete(variables);

      // Invalidated on success *and* failure: a 409 means someone else moved the record, so the
      // user must see where it actually is rather than where they thought it was.
      void queryClient.invalidateQueries({ queryKey: decisionKeys.all });
      if (data) {
        void queryClient.invalidateQueries({ queryKey: decisionKeys.detail(data.recommendationId) });
      }
    },
  });
}

export function useClaim() {
  return useDecisionMutation<string>((recommendationId, idempotencyKey) =>
    apiPost<TransitionResponse>(
      `/api/v1/decisions/recommendations/${recommendationId}/claim`,
      undefined,
      { idempotencyKey },
    ),
  );
}

export function useAccept() {
  return useDecisionMutation<string>((decisionId, idempotencyKey) =>
    apiPost<TransitionResponse>(`/api/v1/decisions/${decisionId}/accept`, undefined, { idempotencyKey }),
  );
}

export interface AcceptWithModificationVariables {
  decisionId: string;
  modification: { field: string; from: number; to: number; rationale: string | null };
}

export function useAcceptWithModification() {
  return useDecisionMutation<AcceptWithModificationVariables>((variables, idempotencyKey) =>
    apiPost<TransitionResponse>(
      `/api/v1/decisions/${variables.decisionId}/accept-with-modification`,
      variables.modification,
      { idempotencyKey },
    ),
  );
}

export interface RejectVariables {
  decisionId: string;
  note: string;
}

export function useReject() {
  return useDecisionMutation<RejectVariables>((variables, idempotencyKey) =>
    apiPost<TransitionResponse>(
      `/api/v1/decisions/${variables.decisionId}/reject`,
      { note: variables.note },
      { idempotencyKey },
    ),
  );
}

export interface SignOffVariables {
  decisionId: string;
  stepNumber: number;
  approved: boolean;
  note: string | null;
}

export function useSignOff() {
  return useDecisionMutation<SignOffVariables>((variables, idempotencyKey) =>
    apiPost<TransitionResponse>(
      `/api/v1/decisions/${variables.decisionId}/approvals/${variables.stepNumber}`,
      { approved: variables.approved, note: variables.note },
      { idempotencyKey },
    ),
  );
}

export function useMarkImplemented() {
  return useDecisionMutation<string>((decisionId, idempotencyKey) =>
    apiPost<TransitionResponse>(`/api/v1/decisions/${decisionId}/implemented`, undefined, {
      idempotencyKey,
    }),
  );
}

export interface RecordOutcomeVariables {
  decisionId: string;
  metric: string;
  realisedValue: number;
  unit: string | null;
  note: string | null;
}

export function useRecordOutcome() {
  return useDecisionMutation<RecordOutcomeVariables>((variables, idempotencyKey) =>
    apiPost<TransitionResponse>(
      `/api/v1/decisions/${variables.decisionId}/outcome`,
      {
        metric: variables.metric,
        realisedValue: variables.realisedValue,
        unit: variables.unit,
        note: variables.note,
      },
      { idempotencyKey },
    ),
  );
}
