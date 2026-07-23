import { Link } from '@tanstack/react-router';
import { PageHeader } from '@/components/ui/PageHeader';
import { StatCard } from '@/components/ui/StatCard';
import { Card, CardHeader } from '@/components/ui/Card';
import { Icon } from '@/components/ui/Icon';
import { LoadingState, ErrorState, EmptyState } from '@/components/ui/states';
import { useDecisionFeed } from '@/lib/api/executive';
import type { Decision, DecisionSeverity } from '@/lib/api/executive';
import { navItemById } from '@/config/navigation';
import { fmtSar } from '@/lib/format';

/** Severity → risk token class. Severity is always also stated in text, never colour alone. */
const severityClass: Record<DecisionSeverity, string> = {
  High: 'risk-crit',
  Medium: 'risk-high',
  Low: 'risk-med',
};

/** Executive-facing wording for a severity, matching the v3 cockpit vocabulary. */
const severityLabel: Record<DecisionSeverity, string> = {
  High: 'Critical',
  Medium: 'Attention',
  Low: 'Monitor',
};

function DecisionCard({ decision }: { decision: Decision }) {
  const target = navItemById(decision.screen);

  return (
    <Card className="decision-card">
      <div className="decision-card__head">
        <span className={`badge ${severityClass[decision.severity]}`}>
          <span className="badge__dot" />
          {severityLabel[decision.severity]}
        </span>
        <span className="badge">{decision.area}</span>
        {decision.isDemo ? (
          <span className="badge badge--demo" title="Derived from synthetic demo data, not Oracle Fusion">
            <Icon name="biotech" />
            Demo data
          </span>
        ) : null}
        <span className="decision-card__priority" title="Priority: impact × urgency × confidence × controllability">
          <span className="decision-card__priority-value">{decision.priority}</span>
          <span className="decision-card__priority-label">priority</span>
        </span>
      </div>

      <h3 className="decision-card__title">{decision.title}</h3>
      <p className="decision-card__why">{decision.whyNow}</p>

      <dl className="decision-card__meta">
        <div>
          <dt>{decision.kind === 'Opportunity' ? 'Opportunity' : 'Exposure'}</dt>
          <dd className="decision-card__money">{fmtSar(decision.impactSar)}</dd>
        </div>
        <div>
          <dt>Confidence</dt>
          <dd>
            {decision.confidence} <span className="decision-card__pct">({decision.confidencePct}%)</span>
          </dd>
        </div>
        <div>
          <dt>Owner</dt>
          <dd>{decision.ownerRole}</dd>
        </div>
        <div>
          <dt>Review within</dt>
          <dd>{decision.dueDays} days</dd>
        </div>
      </dl>

      <p className="decision-card__action">
        <Icon name="lightbulb" />
        {decision.action}
      </p>

      <div className="decision-card__drivers">
        {decision.factors.map((f) => (
          <span key={f.name} className="driver">
            <span className="driver__label">{f.name}</span>
            <span className="driver__bar" aria-hidden="true">
              <span className="driver__fill" style={{ width: `${f.percent}%` }} />
            </span>
            <span className="driver__pct">{f.percent}%</span>
          </span>
        ))}
      </div>

      <div className="decision-card__foot">
        <span className="decision-card__evidence">{decision.evidence}</span>
        {target ? (
          <Link to={target.path} className="decision-card__link">
            Open {target.label}
            <Icon name="arrow_forward" />
          </Link>
        ) : null}
      </div>
    </Card>
  );
}

/** UC8 — Executive Decision Cockpit. The decisions needing attention this period,
 *  ranked across every intelligence module. */
export default function ExecutiveCockpit() {
  const { data, isLoading, isError, error, refetch } = useDecisionFeed();

  const summary = data?.summary;

  return (
    <>
      <PageHeader
        title="Decision Cockpit"
        summary="The decisions that need attention this month — why, impact, owner and evidence."
        useCase="UC8"
        wireframed
        meta={[
          { label: 'Data source', value: 'Sample dataset (not live Oracle Fusion)' },
          {
            label: 'Last refreshed',
            value: data ? new Date(data.generatedAtUtc).toLocaleString() : '—',
          },
          { label: 'Implementation', value: 'Operational' },
        ]}
      />

      <div className="grid grid--stats">
        <StatCard
          label="Decisions to review"
          value={summary ? summary.total : '—'}
          icon="fact_check"
          hint={summary ? `${summary.dueThisWeek} due within a week` : 'Awaiting assessment'}
        />
        <StatCard
          label="Critical"
          value={summary ? summary.critical : '—'}
          icon="warning"
          hint={summary ? `${summary.lowConfidence} low confidence` : 'Awaiting assessment'}
        />
        <StatCard
          label="Exposure at risk"
          value={summary ? fmtSar(summary.riskValueSar) : 'SAR —'}
          icon="trending_down"
          hint="Across inventory, procurement and parts"
        />
        <StatCard
          label="Opportunity value"
          value={summary ? fmtSar(summary.opportunityValueSar) : 'SAR —'}
          icon="trending_up"
          hint="Demand the current plan does not cover"
        />
      </div>

      <div style={{ height: 'var(--gap)' }} />

      <Card>
        <CardHeader
          title="Decisions needing attention"
          subtitle={data ? data.narrative : 'Ranked by impact, urgency, confidence and controllability'}
        />

        {isLoading ? (
          <LoadingState label="Assessing every module…" />
        ) : isError ? (
          <ErrorState
            title="Could not load the decision feed"
            message={
              error instanceof Error
                ? error.message
                : 'The BeeEye API did not respond. Start the API host and try again.'
            }
            onRetry={() => void refetch()}
          />
        ) : !data || data.decisions.length === 0 ? (
          <EmptyState
            title="No decisions need attention"
            message="No module reported a material exception for this analysis period."
            icon="task_alt"
          />
        ) : (
          <div className="decision-list">
            {data.decisions.map((d) => (
              <DecisionCard key={d.id} decision={d} />
            ))}
          </div>
        )}

        {data && data.gaps.length > 0 ? (
          <div className="decision-gaps" role="status">
            <Icon name="error" />
            <div>
              <strong>This view is incomplete.</strong>
              <ul>
                {data.gaps.map((g) => (
                  <li key={g.area}>
                    <strong>{g.area}:</strong> {g.reason}
                  </li>
                ))}
              </ul>
            </div>
          </div>
        ) : null}
      </Card>
    </>
  );
}
