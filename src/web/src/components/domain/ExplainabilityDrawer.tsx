import { useId, useState } from 'react';
import type { ReactNode } from 'react';
import { Drawer } from '@/components/ui/Drawer';
import { Icon } from '@/components/ui/Icon';
import { AiLabel } from '@/components/ui/AiLabel';
import { LoadingState, ErrorState } from '@/components/ui/states';
import { EvidenceChart } from '@/components/charts/EvidenceChart';
import { DecisionFooter } from '@/components/domain/DecisionFooter';
import { ApiError, apiErrorMessage } from '@/lib/api/client';
import { useCurrentUser } from '@/lib/api/identity';
import {
  feedbackVerdicts,
  lineageIcons,
  useExplanation,
  useSubmitFeedback,
} from '@/lib/api/explainability';
import type {
  ConfidenceStatement,
  Driver,
  Explanation,
  ExplanationGap,
  ExplainSubject,
  FeedbackVerdict,
  ImpactTile,
  LineageNode,
  ModelInfo,
  Ownership,
} from '@/lib/api/explainability';

/** Above this many drivers the panel becomes a scroll trap, so the rest go behind a disclosure. */
const VISIBLE_DRIVERS = 8;

interface ExplainabilityDrawerProps {
  /** The subject to explain. `null` keeps the drawer closed and the query unsent. */
  subject: ExplainSubject | null;
  onClose: () => void;
}

// ---------------------------------------------------------------------------
// Sections
// ---------------------------------------------------------------------------

interface SectionProps {
  title: string;
  /** Appended to the heading in a lighter weight, e.g. the evidence period. */
  suffix?: string;
  children: ReactNode;
}

/**
 * One of v3's eleven sections.
 *
 * A real `<section aria-labelledby>` rather than a styled `<div>`: a screen-reader user navigating
 * by landmark gets the same eleven-part structure a sighted user gets by scrolling.
 */
function Section({ title, suffix, children }: SectionProps) {
  const id = useId();
  return (
    <section className="ex__section" aria-labelledby={id}>
      <h3 id={id} className="ex__section-title">
        {title}
        {suffix ? <span> · {suffix}</span> : null}
      </h3>
      {children}
    </section>
  );
}

function ImpactSection({ impacts }: { impacts: ImpactTile[] }) {
  if (impacts.length === 0) return null;

  return (
    <Section title="Expected impact">
      <div className="ex__tiles">
        {impacts.map((tile) => (
          <div key={tile.label} className={`ex__tile ex__tile--${tile.tone}`}>
            <div className="ex__tile-label">{tile.label}</div>
            {/* Pre-formatted invariantly on the server. Never re-formatted here — the browser does
                not know whether this is money, a count or a range. */}
            <div className="ex__tile-value">{tile.value}</div>
          </div>
        ))}
      </div>
    </Section>
  );
}

function ConfidenceSection({ confidence }: { confidence: ConfidenceStatement | null }) {
  // Omitted entirely when the engine computed no band. v3 defaults a missing confidence to "Medium";
  // asserting a band nobody computed is a correctness failure dressed as a default.
  if (confidence === null) return null;

  return (
    <Section title="Confidence">
      <div className="ex__panel">
        <div className={`ex__confidence-head ex__confidence--${confidence.band.toLowerCase()}`}>
          <span className="ex__dot" aria-hidden="true" />
          <b className="ex__confidence-band">{confidence.band}</b>
          {confidence.percent !== null ? (
            <span className="ex__confidence-pct">{confidence.percent}%</span>
          ) : null}
        </div>
        {confidence.why.length > 0 ? (
          <ul className="ex__reasons">
            {confidence.why.map((reason) => (
              <li key={reason}>
                <Icon name="chevron_right" />
                {reason}
              </li>
            ))}
          </ul>
        ) : null}
      </div>
    </Section>
  );
}

function DriversSection({ drivers }: { drivers: Driver[] }) {
  const [expanded, setExpanded] = useState(false);

  if (drivers.length === 0) return null;

  const shown = expanded ? drivers : drivers.slice(0, VISIBLE_DRIVERS);
  const hidden = drivers.length - shown.length;

  return (
    <Section title="Top drivers">
      <ol className="ex__drivers">
        {shown.map((driver, index) => (
          <li key={driver.label} className="ex__driver">
            <span className="ex__driver-n" aria-hidden="true">
              {index + 1}
            </span>
            <div className="ex__driver-body">
              <div className="ex__driver-label">{driver.label}</div>
              {driver.detail ? <div className="ex__driver-detail">{driver.detail}</div> : null}
            </div>
          </li>
        ))}
      </ol>
      {hidden > 0 || expanded ? (
        <button
          type="button"
          className="ex__disclosure"
          aria-expanded={expanded}
          onClick={() => setExpanded((value) => !value)}
        >
          {expanded ? 'Show fewer drivers' : `Show all (${drivers.length})`}
        </button>
      ) : null}
    </Section>
  );
}

function EvidenceSection({ explanation }: { explanation: Explanation }) {
  const evidence = explanation.evidence;
  if (evidence === null || evidence.points.length === 0) return null;

  return (
    <Section title="Historical evidence" suffix={evidence.period}>
      <div className="ex__panel">
        <EvidenceChart series={evidence} />
        {evidence.note ? <p className="ex__note">{evidence.note}</p> : null}
      </div>
    </Section>
  );
}

function AssumptionsSection({ assumptions }: { assumptions: string[] }) {
  if (assumptions.length === 0) return null;

  return (
    <Section title="Assumptions">
      <ul className="ex__assumptions">
        {assumptions.map((assumption) => (
          <li key={assumption}>
            <Icon name="info" />
            {assumption}
          </li>
        ))}
      </ul>
    </Section>
  );
}

function LineageSection({ lineage }: { lineage: LineageNode[] }) {
  if (lineage.length === 0) return null;

  return (
    <Section title="Data lineage">
      <div className="ex__chips">
        {lineage.map((node) => (
          <span key={`${node.kind}-${node.label}`} className={`ex__chip ex__chip--${node.kind}`}>
            <Icon name={lineageIcons[node.kind] ?? 'calculate'} />
            {node.label}
          </span>
        ))}
      </div>
    </Section>
  );
}

function ModelSection({ model }: { model: ModelInfo | null }) {
  if (model === null) return null;

  const rows: { label: string; value: string; mono?: boolean }[] = [
    { label: 'Model', value: model.name },
    { label: 'Version', value: model.version, mono: true },
    { label: 'Recalculated', value: model.recalculated },
    { label: 'Horizon', value: model.horizon },
    { label: 'Validation', value: model.validation },
    { label: 'Error', value: model.error, mono: true },
  ];

  return (
    <Section title="Model / rule information">
      <dl className="ex__model ex__panel">
        {rows.map((row) => (
          <div key={row.label}>
            <dt>{row.label}</dt>
            <dd className={row.mono ? 'mono' : undefined}>{row.value}</dd>
          </div>
        ))}
      </dl>
    </Section>
  );
}

function OwnershipSection({ ownership }: { ownership: Ownership | null }) {
  if (ownership === null) return null;

  return (
    <Section title="Ownership">
      <dl className="ex__ownership">
        <div>
          <dt>Owner</dt>
          <dd>{ownership.ownerRole}</dd>
        </div>
        <div>
          <dt>Status</dt>
          <dd>{ownership.status}</dd>
        </div>
      </dl>
    </Section>
  );
}

// ---------------------------------------------------------------------------
// Section 11 — "Was this useful?"
// ---------------------------------------------------------------------------

interface FeedbackSectionProps {
  subject: ExplainSubject;
  current: FeedbackVerdict | null;
  caveat: string;
}

/**
 * v3's four verdicts, **persisted**.
 *
 * The prototype's caption says the answer "is recorded in the analytics platform only" while
 * `explainFeedback()` writes to component state and loses it on reload — the exact pattern ADR-0006
 * rejects, and a control that silently discards input is worse than no control. It writes to a real
 * append-only table now (`POST /api/v1/predictions/explain/feedback`).
 *
 * **No optimistic update.** The server may refuse, and marking a verdict "submitted" before it is
 * would be the same lie in a smaller box.
 */
function FeedbackSection({ subject, current, caveat }: FeedbackSectionProps) {
  const [note, setNote] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [confirmed, setConfirmed] = useState<string | null>(null);
  const noteId = useId();

  const submit = useSubmitFeedback();

  const send = (verdict: FeedbackVerdict) => {
    setError(null);
    setConfirmed(null);

    submit
      .mutateAsync({ kind: subject.kind, ref: subject.ref, verdict, note: note.trim() || null })
      .then(() => {
        setConfirmed('Thank you — your feedback was recorded.');
        setNote('');
      })
      .catch((e: unknown) => setError(apiErrorMessage(e)));
  };

  return (
    <Section title="Was this useful?">
      <div className="ex__verdicts" role="group" aria-label="Was this explanation useful?">
        {feedbackVerdicts.map((verdict) => (
          <button
            key={verdict.value}
            type="button"
            className="ex__verdict"
            aria-pressed={current === verdict.value}
            disabled={submit.isPending}
            onClick={() => send(verdict.value)}
          >
            <Icon name={verdict.icon} />
            {verdict.label}
          </button>
        ))}
      </div>

      <label className="ex__note-field" htmlFor={noteId}>
        Anything to add? (optional)
        <textarea
          id={noteId}
          rows={2}
          maxLength={1000}
          value={note}
          onChange={(e) => setNote(e.target.value)}
        />
      </label>

      {/* v3's caption, kept verbatim in substance — and now true. */}
      <p className="ex__caption">{caveat}</p>

      <p className="ex__status ex__status--error" role="alert">
        {error}
      </p>
      <p className="ex__status ex__status--ok" role="status" aria-live="polite">
        {submit.isPending ? 'Recording…' : confirmed}
      </p>
    </Section>
  );
}

// ---------------------------------------------------------------------------
// The panel
// ---------------------------------------------------------------------------

function Gaps({ gaps }: { gaps: ExplanationGap[] }) {
  if (gaps.length === 0) return null;

  return (
    <div className="ex__gaps" role="status">
      <strong>This explanation is incomplete.</strong>
      <ul>
        {gaps.map((gap) => (
          <li key={gap.area}>
            {gap.area}: {gap.reason}
          </li>
        ))}
      </ul>
    </div>
  );
}

function Body({ subject }: { subject: ExplainSubject }) {
  const query = useExplanation(subject);
  const subjectId = useCurrentUser().data?.subjectId ?? null;

  if (query.isLoading) {
    return <LoadingState label="Assembling the explanation…" />;
  }

  if (query.isError) {
    const forbidden = query.error instanceof ApiError && query.error.status === 403;
    const missing = query.error instanceof ApiError && query.error.status === 404;

    if (forbidden) {
      return (
        <ErrorState
          title="You do not have access to this explanation"
          message="Seeing why a figure says what it says needs the same permission as seeing the figure. Ask an administrator to grant it."
        />
      );
    }

    if (missing) {
      // Not an error state dressed up: the subject genuinely has nothing recorded, and saying so
      // plainly beats a blank panel or a red box.
      return (
        <div className="state">
          <Icon name="search_off" className="state__icon" />
          <strong>No recorded explanation</strong>
          <span>{apiErrorMessage(query.error)}</span>
        </div>
      );
    }

    return (
      <ErrorState
        title="Could not load this explanation"
        message={apiErrorMessage(query.error)}
        onRetry={() => void query.refetch()}
      />
    );
  }

  const data = query.data;
  if (!data) return null;

  const explanation = data.explanation;

  if (explanation === null) {
    // Every provider that could have answered failed. A gap must never read as "nothing to explain".
    return (
      <div className="ex">
        <Gaps gaps={data.gaps} />
        <p className="ex__note">
          The underlying analysis could not be reached, so there is nothing to show yet. This is not
          the same as the figure having no explanation — try again in a moment.
        </p>
      </div>
    );
  }

  // *The caller's own* current verdict — matched on the stable subject id, not simply the newest
  // row. The API returns the latest verdict per submitter, so highlighting `feedback[0]` would show
  // a colleague's answer as though it were yours.
  const mine = data.feedback.find((f) => f.submittedBy === subjectId)?.verdict ?? null;

  return (
    <div className="ex">
      <Gaps gaps={data.gaps} />

      {/* 1 — Output label. Always present. */}
      <AiLabel kind={explanation.label} />

      {explanation.isDemoData ? <AiLabel kind="demo" /> : null}

      {/* 2 — Recommendation. */}
      {explanation.recommendation ? (
        <Section title="Recommendation">
          <div className="ex__callout">{explanation.recommendation}</div>
        </Section>
      ) : null}

      {/* 3 — Expected impact. */}
      <ImpactSection impacts={explanation.impacts} />

      {/* 4 — Confidence. */}
      <ConfidenceSection confidence={explanation.confidence} />

      {/* 5 — Top drivers. */}
      <DriversSection drivers={explanation.drivers} />

      {/* 6 — Historical evidence. */}
      <EvidenceSection explanation={explanation} />

      {/* 7 — Assumptions. */}
      <AssumptionsSection assumptions={explanation.assumptions} />

      {/* 8 — Data lineage. */}
      <LineageSection lineage={explanation.lineage} />

      {/* 9 — Model / rule information. */}
      <ModelSection model={explanation.model} />

      {/* 10 — Ownership. */}
      <OwnershipSection ownership={explanation.ownership} />

      {/* 11 — Was this useful? */}
      <FeedbackSection subject={subject} current={mine} caveat={data.feedbackCaveat} />
    </div>
  );
}

/**
 * The global explainability drawer (V3-DS-006) — one panel that answers "why?" for any number,
 * forecast or recommendation the platform shows.
 *
 * Built **on** the shared `Drawer`, which already provides the overlay, Escape, the focus trap, focus
 * restoration and the pinned footer. Nothing here forks it; the only geometry change is the
 * `drawer--explain` modifier carrying v3's 474px / 94vw.
 *
 * **Nothing in this panel is authored by a model.** It renders what the deterministic engine already
 * computed (ADR-0006 §2.6, `overview.md` §8 — *GenAI narrates, never decides*). Live narration is
 * S10; no model call ships here.
 */
export function ExplainabilityDrawer({ subject, onClose }: ExplainabilityDrawerProps) {
  const query = useExplanation(subject);
  const explanation = query.data?.explanation ?? null;

  return (
    <Drawer
      open={subject !== null}
      // Falls back to the subject reference while the payload is in flight, so the panel is never
      // an anonymous box — it opens immediately and fills in.
      title={explanation?.title ?? subject?.ref ?? 'Explanation'}
      className="drawer--explain"
      onClose={onClose}
      header={
        <div className="ex__intro">
          <span className="ex__mark">
            <Icon name="auto_awesome" />
          </span>
          <div className="ex__heading">
            <p className="ex__title">{explanation?.title ?? subject?.ref ?? 'Explanation'}</p>
            <p className="ex__subtitle">
              Why this recommendation?{explanation ? ` · ${explanation.module}` : ''}
            </p>
          </div>
        </div>
      }
      footer={
        // Workflow actions only where the subject *is* a decision. DecisionFooter owns the claim
        // logic and the "no record exists yet" copy; duplicating either here would give the platform
        // two answers to the same question.
        explanation?.ownership ? <DecisionFooter subjectRef={explanation.title} /> : undefined
      }
    >
      {subject ? <Body subject={subject} /> : null}
    </Drawer>
  );
}

interface ExplainButtonProps {
  /** Names the subject in the accessible name, e.g. "Why this recommendation? ES 350 ZX". */
  label: string;
  onClick: () => void;
  className?: string;
}

/**
 * The trigger every screen renders.
 *
 * A real `<button>` whose accessible name states the subject — never a bare icon. An icon-only
 * control here fails a screen-reader user (nine identical "info" buttons on one screen) and a touch
 * user (below the 44px target), and this control appears on every row of every table.
 */
export function ExplainButton({ label, onClick, className }: ExplainButtonProps) {
  return (
    <button
      type="button"
      className={className ? `ex-trigger ${className}` : 'ex-trigger'}
      aria-label={`Why this recommendation? ${label}`}
      onClick={onClick}
    >
      <Icon name="auto_awesome" />
      Why?
    </button>
  );
}
