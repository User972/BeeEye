import { useMemo, useState } from 'react';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card } from '@/components/ui/Card';
import { Icon } from '@/components/ui/Icon';
import { Drawer } from '@/components/ui/Drawer';
import { LoadingState, ErrorState, EmptyState } from '@/components/ui/states';
import { ApiError, apiErrorMessage } from '@/lib/api/client';
import { permissions, useCurrentUser, useHasPermission } from '@/lib/api/identity';
import {
  actionLabels,
  modifiableFields,
  statusColours,
  statusLabels,
  statusOrder,
  useAccept,
  useAcceptWithModification,
  useClaim,
  useDecisionDetail,
  useDecisionLog,
  useMarkImplemented,
  useRecordOutcome,
  useReject,
  useSignOff,
} from '@/lib/api/decisions';
import type {
  DecisionAction,
  DecisionDetail,
  DecisionLogItem,
  RecommendationStatus,
} from '@/lib/api/decisions';
import { downloadCsv, toCsv } from '@/lib/csv';
import { fmtSar } from '@/lib/format';

const PAGE_SIZE = 50;

/** Actions that need input before they can be sent. */
type FormAction = 'reject' | 'accept-with-modification' | 'sign-off' | 'record-outcome';

const FORM_ACTIONS: readonly DecisionAction[] = [
  'reject',
  'accept-with-modification',
  'sign-off',
  'record-outcome',
];

function isFormAction(action: DecisionAction): action is FormAction {
  return FORM_ACTIONS.includes(action);
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
}

// ---------------------------------------------------------------------------
// Status chips
// ---------------------------------------------------------------------------

interface ChipRowProps {
  counts: Record<string, number>;
  active: RecommendationStatus | null;
  onSelect: (status: RecommendationStatus | null) => void;
}

/**
 * v3's status chip row. A chip with a zero count is hidden unless it is the active filter — v3's
 * behaviour, and it keeps the row from becoming a wall of zeroes on a young dataset.
 */
function ChipRow({ counts, active, onSelect }: ChipRowProps) {
  const visible = statusOrder.filter((status) => (counts[status] ?? 0) > 0 || active === status);

  if (visible.length === 0) return null;

  return (
    <div className="dl-chips" role="group" aria-label="Filter by status">
      {visible.map((status) => (
        <button
          key={status}
          type="button"
          className={`dl-chip${active === status ? ' dl-chip--active' : ''}`}
          aria-pressed={active === status}
          onClick={() => onSelect(active === status ? null : status)}
        >
          <span className="dl-chip__dot" style={{ background: statusColours[status] }} aria-hidden="true" />
          <span className="dl-chip__label">{statusLabels[status]}</span>
          <b className="dl-chip__count">{counts[status] ?? 0}</b>
        </button>
      ))}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Row action forms
// ---------------------------------------------------------------------------

interface FormProps {
  action: FormAction;
  item: DecisionLogItem;
  busy: boolean;
  onCancel: () => void;
  onSubmit: (payload: FormPayload) => void;
}

type FormPayload =
  | { kind: 'reject'; note: string }
  | { kind: 'accept-with-modification'; field: string; from: number; to: number; rationale: string | null }
  | { kind: 'sign-off'; approved: boolean; note: string | null }
  | { kind: 'record-outcome'; metric: string; realisedValue: number; unit: string | null; note: string | null };

/**
 * The input an action needs before it can be sent.
 *
 * Client-side checks here are a convenience only. Every rule is enforced again on the server, and
 * the server's refusal is what the row displays — see the DoD's "validation is server-authoritative".
 */
function ActionForm({ action, item, busy, onCancel, onSubmit }: FormProps) {
  const [note, setNote] = useState('');
  const [field, setField] = useState<string>(modifiableFields[0].value);
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [metric, setMetric] = useState('');
  const [value, setValue] = useState('');
  const [unit, setUnit] = useState('SAR');
  const [localError, setLocalError] = useState<string | null>(null);

  const submit = (event: React.FormEvent) => {
    event.preventDefault();
    setLocalError(null);

    if (action === 'reject') {
      if (note.trim().length === 0) {
        setLocalError('A reason is required to reject a recommendation.');
        return;
      }
      onSubmit({ kind: 'reject', note: note.trim() });
      return;
    }

    if (action === 'accept-with-modification') {
      const fromValue = Number(from);
      const toValue = Number(to);
      if (from.trim() === '' || to.trim() === '' || Number.isNaN(fromValue) || Number.isNaN(toValue)) {
        setLocalError('Enter the recommended value and the value you are changing it to.');
        return;
      }
      onSubmit({
        kind: 'accept-with-modification',
        field,
        from: fromValue,
        to: toValue,
        rationale: note.trim() === '' ? null : note.trim(),
      });
      return;
    }

    if (action === 'record-outcome') {
      const realised = Number(value);
      if (metric.trim() === '' || value.trim() === '' || Number.isNaN(realised)) {
        setLocalError('Name the metric and the value that was measured.');
        return;
      }
      onSubmit({
        kind: 'record-outcome',
        metric: metric.trim(),
        realisedValue: realised,
        unit: unit.trim() === '' ? null : unit.trim(),
        note: note.trim() === '' ? null : note.trim(),
      });
    }
  };

  const inputId = (name: string) => `${name}-${item.recommendationId}`;

  return (
    <form className="dl-form" onSubmit={submit}>
      {action === 'accept-with-modification' ? (
        <>
          <div className="dl-form__row">
            <label htmlFor={inputId('field')}>Value to change</label>
            <select id={inputId('field')} value={field} onChange={(e) => setField(e.target.value)}>
              {modifiableFields.map((f) => (
                <option key={f.value} value={f.value}>
                  {f.label}
                </option>
              ))}
            </select>
          </div>
          <div className="dl-form__row">
            <label htmlFor={inputId('from')}>Recommended</label>
            <input
              id={inputId('from')}
              inputMode="decimal"
              value={from}
              onChange={(e) => setFrom(e.target.value)}
            />
          </div>
          <div className="dl-form__row">
            <label htmlFor={inputId('to')}>Changed to</label>
            <input id={inputId('to')} inputMode="decimal" value={to} onChange={(e) => setTo(e.target.value)} />
          </div>
          <p className="dl-form__hint">
            A discount must stay within the historically observed 0–20% range. The original
            recommendation is never altered — your change is stored beside it.
          </p>
        </>
      ) : null}

      {action === 'record-outcome' ? (
        <>
          <div className="dl-form__row">
            <label htmlFor={inputId('metric')}>Metric</label>
            <input
              id={inputId('metric')}
              value={metric}
              placeholder="Holding cost avoided"
              onChange={(e) => setMetric(e.target.value)}
            />
          </div>
          <div className="dl-form__row">
            <label htmlFor={inputId('value')}>Realised value</label>
            <input id={inputId('value')} inputMode="decimal" value={value} onChange={(e) => setValue(e.target.value)} />
          </div>
          <div className="dl-form__row">
            <label htmlFor={inputId('unit')}>Unit</label>
            <input id={inputId('unit')} value={unit} onChange={(e) => setUnit(e.target.value)} />
          </div>
        </>
      ) : null}

      {action !== 'sign-off' ? (
        <div className="dl-form__row dl-form__row--wide">
          <label htmlFor={inputId('note')}>
            {action === 'reject' ? 'Reason (required)' : 'Note'}
          </label>
          <textarea
            id={inputId('note')}
            rows={2}
            value={note}
            onChange={(e) => setNote(e.target.value)}
            aria-required={action === 'reject'}
          />
        </div>
      ) : (
        <div className="dl-form__row dl-form__row--wide">
          <label htmlFor={inputId('note')}>Note</label>
          <textarea id={inputId('note')} rows={2} value={note} onChange={(e) => setNote(e.target.value)} />
        </div>
      )}

      {localError ? (
        <p className="dl-form__error" role="alert">
          {localError}
        </p>
      ) : null}

      <div className="dl-form__actions">
        {action === 'sign-off' ? (
          <>
            <button
              type="button"
              className="dl-btn dl-btn--primary"
              disabled={busy}
              onClick={() => onSubmit({ kind: 'sign-off', approved: true, note: note.trim() || null })}
            >
              Approve step
            </button>
            <button
              type="button"
              className="dl-btn"
              disabled={busy}
              onClick={() => onSubmit({ kind: 'sign-off', approved: false, note: note.trim() || null })}
            >
              Decline step
            </button>
          </>
        ) : (
          <button type="submit" className="dl-btn dl-btn--primary" disabled={busy}>
            {busy ? 'Working…' : actionLabels[action]}
          </button>
        )}
        <button type="button" className="dl-btn" onClick={onCancel} disabled={busy}>
          Cancel
        </button>
      </div>
    </form>
  );
}

// ---------------------------------------------------------------------------
// Row
// ---------------------------------------------------------------------------

interface RowProps {
  item: DecisionLogItem;
  message: string | null;
  error: string | null;
  busy: boolean;
  openForm: DecisionAction | null;
  canApprove: boolean;
  onAct: (item: DecisionLogItem, action: DecisionAction) => void;
  onSubmitForm: (item: DecisionLogItem, payload: FormPayload) => void;
  onCancelForm: () => void;
  onOpenDetail: (item: DecisionLogItem) => void;
}

function DecisionRow({
  item,
  message,
  error,
  busy,
  openForm,
  canApprove,
  onAct,
  onSubmitForm,
  onCancelForm,
  onOpenDetail,
}: RowProps) {
  // Only the transitions the server would actually accept. Anything else is *absent* rather than
  // present-and-disabled: a disabled control that never enables is a support ticket.
  const actions = item.availableActions;

  // One explanatory line where a control is hidden for permission reasons, rather than silence.
  const approvalHidden =
    !canApprove && (item.status === 'UnderReview' || item.status === 'Accepted' || item.status === 'AcceptedModified');

  return (
    <div className="dl-row" style={{ borderLeftColor: statusColours[item.status] }}>
      <div className="dl-row__head">
        <span className="dl-row__id">{item.ruleId}</span>
        <span className="dl-row__title">{item.subjectRef}</span>
        <span className="dl-row__pill">{item.area}</span>
        {item.isDemoData ? (
          <span className="badge badge--demo" title="Derived from synthetic demo data, not Oracle Fusion">
            <Icon name="biotech" />
            Demo data
          </span>
        ) : null}
      </div>

      {item.evidence ? (
        <p className="dl-row__evidence">
          <b>Evidence:</b> {item.evidence}
        </p>
      ) : null}

      <p className="dl-row__meta">
        {item.source} · created {formatDate(item.createdAtUtc)} · expected impact {fmtSar(item.impactSar)}
      </p>

      <div className="dl-row__foot">
        <span className="dl-row__status">
          <span className="dl-chip__dot" style={{ background: statusColours[item.status] }} aria-hidden="true" />
          {statusLabels[item.status]}
        </span>

        {item.decidedBy ? <span className="dl-row__decided">Decided by {item.decidedBy}</span> : null}

        {item.modification ? (
          <span className="dl-row__mod">
            {item.modification.field}: {item.modification.from} → {item.modification.to}
          </span>
        ) : null}

        <div className="dl-actions">
          {actions.map((action) => (
            <button
              key={action}
              type="button"
              className="dl-btn"
              disabled={busy}
              onClick={() => onAct(item, action)}
            >
              {actionLabels[action]}
            </button>
          ))}
          <button type="button" className="dl-btn dl-btn--link" onClick={() => onOpenDetail(item)}>
            Open evidence
            <Icon name="arrow_forward" />
          </button>
        </div>
      </div>

      {approvalHidden ? (
        <p className="dl-row__hint">Accepting, modifying or rejecting this decision requires approver permission.</p>
      ) : null}

      {openForm && isFormAction(openForm) ? (
        <ActionForm
          action={openForm}
          item={item}
          busy={busy}
          onCancel={onCancelForm}
          onSubmit={(payload) => onSubmitForm(item, payload)}
        />
      ) : null}

      {error ? (
        <p className="dl-row__error" role="alert">
          {error}
        </p>
      ) : null}
      {message ? (
        <p className="dl-row__ok" role="status">
          {message}
        </p>
      ) : null}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Detail drawer — the frozen original beside the human decision
// ---------------------------------------------------------------------------

function DetailPanel({ detail }: { detail: DecisionDetail }) {
  const { recommendation: r, decision, approvalSteps, statusEvents, outcome } = detail;

  return (
    <div className="dl-detail">
      <section className="dl-detail__col" aria-labelledby="dl-detail-system">
        <h3 id="dl-detail-system" className="dl-detail__heading">
          What the system recommended
        </h3>

        {r.isDemoData ? (
          <p className="badge badge--demo">
            <Icon name="biotech" />
            Derived from synthetic demo data, not Oracle Fusion
          </p>
        ) : null}

        <dl className="dl-detail__list">
          <dt>Action</dt>
          <dd>{r.action}</dd>
          <dt>Rationale</dt>
          <dd>{r.rationale}</dd>
          <dt>Expected outcome</dt>
          <dd>{r.expectedOutcome}</dd>
          <dt>Confidence</dt>
          <dd>{r.confidence}</dd>
          <dt>Expected impact</dt>
          <dd>{fmtSar(r.impactSar)}</dd>
          <dt>Evidence</dt>
          <dd>
            <ul>
              {r.evidence.map((e) => (
                <li key={e}>{e}</li>
              ))}
            </ul>
          </dd>
          <dt>Assumptions</dt>
          <dd>
            {r.assumptions.length === 0 ? (
              '—'
            ) : (
              <ul>
                {r.assumptions.map((a) => (
                  <li key={a}>{a}</li>
                ))}
              </ul>
            )}
          </dd>
          <dt>Provenance</dt>
          <dd>
            Ruleset {r.rulesetVersion} · dataset {r.datasetVersion} · analysed {r.analysisDate}
          </dd>
        </dl>
      </section>

      <section className="dl-detail__col" aria-labelledby="dl-detail-human">
        <h3 id="dl-detail-human" className="dl-detail__heading">
          What the human decided
        </h3>

        {decision === null ? (
          <p className="dl-detail__none">
            Nobody has claimed this recommendation yet, so there is no human decision to show.
          </p>
        ) : (
          <dl className="dl-detail__list">
            <dt>Outcome</dt>
            <dd>{decision.outcome}</dd>
            <dt>Claimed by</dt>
            <dd>{decision.openedBy}</dd>
            <dt>Decided by</dt>
            <dd>{decision.decidedBy ?? 'Not yet decided'}</dd>
            {decision.modification ? (
              <>
                <dt>Modification</dt>
                <dd>
                  {decision.modification.field}: {decision.modification.from} → {decision.modification.to}
                  {decision.modification.rationale ? ` — ${decision.modification.rationale}` : ''}
                </dd>
              </>
            ) : null}
            <dt>Note</dt>
            <dd>{decision.note ?? '—'}</dd>
            <dt>Implemented by</dt>
            <dd>{decision.implementedBy ?? 'Not yet implemented'}</dd>
          </dl>
        )}

        <h4 className="dl-detail__subheading">Approval chain</h4>
        {approvalSteps.length === 0 ? (
          <p className="dl-detail__none">No approval steps.</p>
        ) : (
          <ul className="dl-detail__steps">
            {approvalSteps.map((s) => (
              <li key={s.stepNumber}>
                Step {s.stepNumber} · {s.approverRole} — <b>{s.status}</b>
                {s.actedBy ? ` by ${s.actedBy}` : ''}
                {s.note ? ` — ${s.note}` : ''}
              </li>
            ))}
          </ul>
        )}

        <h4 className="dl-detail__subheading">Status history</h4>
        <ol className="dl-detail__timeline">
          {statusEvents.map((e) => (
            <li key={`${e.atUtc}-${e.toStatus}`}>
              <b>{statusLabels[e.toStatus]}</b> · {e.actor} · {formatDate(e.atUtc)}
              {e.reason ? <span className="dl-detail__reason">{e.reason}</span> : null}
            </li>
          ))}
        </ol>

        <h4 className="dl-detail__subheading">Realised outcome</h4>
        {outcome === null ? (
          <p className="dl-detail__none">Not yet measured.</p>
        ) : (
          <p>
            {outcome.metric}: {outcome.realisedValue} {outcome.unit ?? ''} — recorded by {outcome.recordedBy}
          </p>
        )}
      </section>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Screen
// ---------------------------------------------------------------------------

/**
 * V3-GOV-001 — the Decision Log.
 *
 * v3's visual design and status vocabulary, backed by ADR-0006's append-only model. The prototype's
 * free status dropdown is replaced by guard-validated transitions the server publishes per row, and
 * its delete button by a terminal state. There is no delete control anywhere on this screen, and no
 * endpoint behind one.
 */
export default function DecisionLog() {
  const [status, setStatus] = useState<RecommendationStatus | null>(null);
  const [openForm, setOpenForm] = useState<{ id: string; action: DecisionAction } | null>(null);
  const [detailFor, setDetailFor] = useState<DecisionLogItem | null>(null);
  const [rowMessage, setRowMessage] = useState<Record<string, string>>({});
  const [rowError, setRowError] = useState<Record<string, string>>({});
  const [bulkMessage, setBulkMessage] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const log = useDecisionLog({ status, page: 1, pageSize: PAGE_SIZE });
  const detail = useDecisionDetail(detailFor?.recommendationId ?? null);
  const { isLoading: identityLoading } = useCurrentUser();
  const canApprove = useHasPermission(permissions.recommendationApprove);
  const canReview = useHasPermission(permissions.recommendationReview);

  const claim = useClaim();
  const accept = useAccept();
  const acceptModified = useAcceptWithModification();
  const reject = useReject();
  const signOff = useSignOff();
  const markImplemented = useMarkImplemented();
  const recordOutcome = useRecordOutcome();

  const items = useMemo(() => log.data?.items ?? [], [log.data]);
  const counts = log.data?.statusCounts ?? {};
  const totalAcrossStatuses = Object.values(counts).reduce((sum, n) => sum + n, 0);

  const settle = (id: string, promise: Promise<{ message: string }>) => {
    setBusyId(id);
    setRowError((prev) => ({ ...prev, [id]: '' }));

    promise
      .then((response) => {
        setRowMessage((prev) => ({ ...prev, [id]: response.message }));
        setOpenForm(null);
      })
      .catch((error: unknown) => {
        // A 409 means someone else moved this record. The server's own explanation is shown, and the
        // invalidation in the mutation hook refetches so the user sees the state it is actually in.
        setRowError((prev) => ({
          ...prev,
          [id]: apiErrorMessage(error, 'The BeeEye API did not respond. Try again in a moment.'),
        }));
        setRowMessage((prev) => ({ ...prev, [id]: '' }));
      })
      .finally(() => setBusyId(null));
  };

  const act = (item: DecisionLogItem, action: DecisionAction) => {
    if (isFormAction(action)) {
      setOpenForm({ id: item.recommendationId, action });
      return;
    }

    const id = item.recommendationId;
    if (action === 'claim') {
      settle(id, claim.mutateAsync(item.recommendationId));
      return;
    }
    if (action === 'accept' && item.decisionId) {
      settle(id, accept.mutateAsync(item.decisionId));
      return;
    }
    if (action === 'implemented' && item.decisionId) {
      settle(id, markImplemented.mutateAsync(item.decisionId));
    }
  };

  const submitForm = (item: DecisionLogItem, payload: FormPayload) => {
    const id = item.recommendationId;
    const decisionId = item.decisionId;
    if (!decisionId) return;

    switch (payload.kind) {
      case 'reject':
        settle(id, reject.mutateAsync({ decisionId, note: payload.note }));
        break;
      case 'accept-with-modification':
        settle(
          id,
          acceptModified.mutateAsync({
            decisionId,
            modification: {
              field: payload.field,
              from: payload.from,
              to: payload.to,
              rationale: payload.rationale,
            },
          }),
        );
        break;
      case 'sign-off':
        settle(
          id,
          signOff.mutateAsync({ decisionId, stepNumber: 1, approved: payload.approved, note: payload.note }),
        );
        break;
      case 'record-outcome':
        settle(
          id,
          recordOutcome.mutateAsync({
            decisionId,
            metric: payload.metric,
            realisedValue: payload.realisedValue,
            unit: payload.unit,
            note: payload.note,
          }),
        );
        break;
    }
  };

  /**
   * v3's "Log current cockpit decisions", corrected.
   *
   * It claims records the engine has **already generated**; it never fabricates a record in the
   * browser the way the prototype's `logAllDecisions()` did. Partial success is reported honestly,
   * because "6 of 8" is the truth and a blanket success toast is not.
   */
  const logAll = async () => {
    const claimable = items.filter((i) => i.availableActions.includes('claim'));
    if (claimable.length === 0) {
      setBulkMessage('Every recommendation on this page is already claimed.');
      return;
    }

    setBulkMessage(`Claiming ${claimable.length}…`);

    let claimed = 0;
    let taken = 0;
    for (const item of claimable) {
      try {
        await claim.mutateAsync(item.recommendationId);
        claimed += 1;
      } catch (error) {
        if (error instanceof ApiError && error.status === 409) {
          taken += 1;
        }
      }
    }

    const failed = claimable.length - claimed - taken;
    setBulkMessage(
      `${claimed} of ${claimable.length} claimed` +
        (taken > 0 ? `; ${taken} were already claimed by someone else` : '') +
        (failed > 0 ? `; ${failed} could not be claimed` : '') +
        '.',
    );
  };

  const exportCsv = () => {
    downloadCsv(
      'admc_decision_log.csv',
      toCsv(
        [
          { header: 'id', value: (i: DecisionLogItem) => i.ruleId },
          { header: 'decision', value: (i: DecisionLogItem) => i.subjectRef },
          { header: 'module', value: (i: DecisionLogItem) => i.area },
          { header: 'source', value: (i: DecisionLogItem) => i.source },
          { header: 'created', value: (i: DecisionLogItem) => i.createdAtUtc },
          { header: 'owner', value: (i: DecisionLogItem) => i.ownerRole },
          { header: 'status', value: (i: DecisionLogItem) => statusLabels[i.status] },
          { header: 'outcome', value: (i: DecisionLogItem) => i.outcome ?? '' },
          { header: 'decided_by', value: (i: DecisionLogItem) => i.decidedBy ?? '' },
          { header: 'priority', value: (i: DecisionLogItem) => i.priority },
          { header: 'expected_impact_sar', value: (i: DecisionLogItem) => i.impactSar },
          { header: 'evidence', value: (i: DecisionLogItem) => i.evidence },
        ],
        items,
      ),
    );
  };

  const forbidden = log.error instanceof ApiError && log.error.status === 403;

  return (
    <>
      <PageHeader
        title="Decision Log"
        summary="What was advised, who decided, what they changed, and what resulted."
        wireframed
        meta={[
          { label: 'Record model', value: 'Append-only (ADR 0006)' },
          { label: 'System of record', value: 'Read-only — never written back' },
          { label: 'Implementation', value: 'Operational' },
        ]}
      />

      <Card>
        <div className="dl-toolbar">
          <ChipRow counts={counts} active={status} onSelect={setStatus} />

          <div className="dl-toolbar__actions">
            {canReview ? (
              <button type="button" className="dl-btn" onClick={() => void logAll()}>
                <Icon name="auto_awesome" />
                Log current cockpit decisions
              </button>
            ) : null}
            <button type="button" className="dl-btn" onClick={exportCsv} disabled={items.length === 0}>
              <Icon name="download" />
              Export
            </button>
          </div>
        </div>

        <p className="dl-banner">
          <Icon name="gavel" />
          <span>
            A governed audit trail of every recommendation. <b>In progress</b> and <b>Completed</b> refer
            to the internal review and action-tracking process — not confirmation that Oracle
            transactions were executed.
          </span>
          {status !== null ? (
            <button type="button" className="dl-btn dl-btn--link" onClick={() => setStatus(null)}>
              Clear filter
            </button>
          ) : null}
        </p>

        {bulkMessage ? (
          <p className="dl-row__ok" role="status">
            {bulkMessage}
          </p>
        ) : null}

        {log.isLoading || identityLoading ? (
          <LoadingState label="Loading the decision log…" />
        ) : forbidden ? (
          <ErrorState
            title="You do not have access to the decision log"
            message="Reviewing recommendations requires the recommendation.review permission. Ask an administrator to grant it."
          />
        ) : log.isError ? (
          <ErrorState
            title="Could not load the decision log"
            message={apiErrorMessage(log.error)}
            onRetry={() => void log.refetch()}
          />
        ) : totalAcrossStatuses === 0 ? (
          <EmptyState
            icon="fact_check"
            title="No decisions logged yet"
            message="Accept a recommendation from the Decision Cockpit or any module, or log the current month's decision set. Entries live only in this analytics platform — they are not written back to Oracle Fusion."
          />
        ) : items.length === 0 ? (
          <div className="state">
            <Icon name="filter_alt_off" className="state__icon" />
            <strong>No decisions match this status filter</strong>
            <button type="button" className="dl-btn" onClick={() => setStatus(null)}>
              Clear filter
            </button>
          </div>
        ) : (
          <div className="dl-rows">
            {items.map((item) => (
              <DecisionRow
                key={item.recommendationId}
                item={item}
                busy={busyId === item.recommendationId}
                message={rowMessage[item.recommendationId] || null}
                error={rowError[item.recommendationId] || null}
                openForm={openForm?.id === item.recommendationId ? openForm.action : null}
                canApprove={canApprove}
                onAct={act}
                onSubmitForm={submitForm}
                onCancelForm={() => setOpenForm(null)}
                onOpenDetail={setDetailFor}
              />
            ))}
          </div>
        )}
      </Card>

      <Drawer
        open={detailFor !== null}
        title={detailFor ? `${detailFor.ruleId} · ${detailFor.subjectRef}` : 'Decision'}
        onClose={() => setDetailFor(null)}
      >
        {detail.isLoading ? (
          <LoadingState label="Loading the decision…" />
        ) : detail.isError || !detail.data ? (
          <ErrorState title="Could not load this decision" message={apiErrorMessage(detail.error)} />
        ) : (
          <DetailPanel detail={detail.data} />
        )}
      </Drawer>
    </>
  );
}
