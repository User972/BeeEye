import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardHeader } from '@/components/ui/Card';
import { Icon } from '@/components/ui/Icon';
import { AiLabel } from '@/components/ui/AiLabel';
import { LoadingState, ErrorState, EmptyState } from '@/components/ui/states';
import { useLineage } from '@/lib/api/lineage';
import type { LineageMetric } from '@/lib/api/lineage';
import { apiErrorMessage } from '@/lib/api/client';
import { downloadCsv, toCsv } from '@/lib/csv';

/** The confirmed/demo chip: a demo metric uses the shared AI label; confirmed is a plain word chip. */
function MetricState({ metric }: { metric: LineageMetric }) {
  if (metric.state === 'demo') {
    return <AiLabel kind="demo" />;
  }
  return (
    <span className="badge risk-low">
      <Icon name="verified" />
      Confirmed
    </span>
  );
}

function exportMetrics(metrics: LineageMetric[]): void {
  downloadCsv(
    'admc_lineage_metrics.csv',
    toCsv<LineageMetric>(
      [
        { header: 'metric', value: (m) => m.metric },
        { header: 'source', value: (m) => m.source },
        { header: 'basis', value: (m) => m.basis },
        { header: 'state', value: (m) => m.state },
      ],
      metrics,
    ),
  );
}

/**
 * Model & Data Lineage (V3-GOV-009). The end-to-end path from Oracle Fusion to a decision, and the
 * source and basis behind every metric — with each metric honestly tagged confirmed or demo.
 */
export default function LineagePage() {
  const { data, isLoading, isError, error, refetch } = useLineage();

  return (
    <>
      <PageHeader
        title="Model & Data Lineage"
        summary="How data flows from Oracle Fusion to a decision, and what each metric is really derived from."
        wireframed
        meta={[
          { label: 'Integration', value: 'Read-only — no write-back to Oracle Fusion' },
          { label: 'Implementation', value: 'Operational' },
        ]}
      />

      <Card>
        <CardHeader
          title="Source-to-decision pipeline"
          subtitle="Governed, read-only extracts from Oracle Fusion through to this decision-intelligence experience."
        />

        {isLoading ? (
          <LoadingState label="Loading the lineage…" />
        ) : isError ? (
          <ErrorState title="Could not load lineage" message={apiErrorMessage(error)} onRetry={() => void refetch()} />
        ) : !data || data.pipeline.length === 0 ? (
          <EmptyState title="No pipeline defined" message="The lineage pipeline is empty." icon="account_tree" />
        ) : (
          <ol className="grid grid--cards" style={{ listStyle: 'none', margin: 0, padding: 0 }}>
            {data.pipeline.map((stage, i) => (
              <li key={stage.title}>
                <Card>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 8 }}>
                    <Icon name={stage.icon} />
                    <strong>
                      {i + 1}. {stage.title}
                    </strong>
                  </div>
                  <p style={{ color: 'var(--text-faint)', margin: 0 }}>{stage.description}</p>
                </Card>
              </li>
            ))}
          </ol>
        )}
      </Card>

      <div style={{ height: 'var(--gap)' }} />

      <Card>
        <CardHeader
          title="Metric provenance"
          subtitle="Every decision metric, its Fusion source and basis, tagged confirmed or synthetic-demo."
          action={
            <button
              type="button"
              className="icon-btn"
              style={{ width: 'auto', padding: '6px 14px' }}
              onClick={() => data && exportMetrics(data.metrics)}
              disabled={!data || data.metrics.length === 0}
            >
              <Icon name="download" />
              Export CSV
            </button>
          }
        />

        {isLoading ? (
          <LoadingState label="Loading the metrics…" />
        ) : isError ? (
          <ErrorState title="Could not load the metrics" message={apiErrorMessage(error)} onRetry={() => void refetch()} />
        ) : !data || data.metrics.length === 0 ? (
          <EmptyState title="No metrics defined" message="No decision metric is registered." icon="dataset" />
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table className="data-table">
              <thead>
                <tr>
                  <th>Metric</th>
                  <th>Source</th>
                  <th>Basis</th>
                  <th>State</th>
                </tr>
              </thead>
              <tbody>
                {data.metrics.map((m) => (
                  <tr key={m.metric}>
                    <td>{m.metric}</td>
                    <td>{m.source}</td>
                    <td>{m.basis}</td>
                    <td>
                      <MetricState metric={m} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </>
  );
}
