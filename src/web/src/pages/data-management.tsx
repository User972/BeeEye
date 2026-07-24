import { PageHeader } from '@/components/ui/PageHeader';
import { StatCard } from '@/components/ui/StatCard';
import { Card, CardHeader } from '@/components/ui/Card';
import { Icon } from '@/components/ui/Icon';
import { AiLabel } from '@/components/ui/AiLabel';
import { LoadingState, ErrorState, EmptyState } from '@/components/ui/states';
import { useDataHealth } from '@/lib/api/dataHealth';
import type { DataSource, DataQualityIssue } from '@/lib/api/dataHealth';
import { apiErrorMessage } from '@/lib/api/client';
import { fmtInt } from '@/lib/format';
import { downloadCsv, toCsv } from '@/lib/csv';

/** Score band → risk token class. The band word is always shown too, never colour alone. */
const scoreBandClass: Record<string, string> = {
  Healthy: 'risk-low',
  Warning: 'risk-med',
  Critical: 'risk-crit',
};

/** Issue severity → token class. Severity is stated in words as well. */
const severityClass: Record<string, string> = {
  high: 'risk-crit',
  medium: 'risk-med',
  ok: 'risk-low',
};

const severityWord: Record<string, string> = {
  high: 'High',
  medium: 'Medium',
  ok: 'OK',
};

/** A source status chip: word + icon + colour (demo uses the shared AI label). */
function SourceStatus({ source }: { source: DataSource }) {
  if (source.statusKind === 'demo') {
    return <AiLabel kind="demo" />;
  }

  const chip: Record<string, { cls: string; icon: string }> = {
    ready: { cls: 'risk-low', icon: 'check_circle' },
    assumptions: { cls: 'risk-med', icon: 'rule' },
    blocked: { cls: 'risk-crit', icon: 'block' },
  };
  const { cls, icon } = chip[source.statusKind] ?? { cls: 'badge', icon: 'help' };

  return (
    <span className={`badge ${cls}`}>
      <Icon name={icon} />
      {source.status}
    </span>
  );
}

function exportSources(sources: DataSource[]): void {
  downloadCsv(
    'admc_data_health_sources.csv',
    toCsv<DataSource>(
      [
        { header: 'source', value: (s) => s.name },
        { header: 'system', value: (s) => s.system },
        { header: 'status', value: (s) => s.status },
        { header: 'rows', value: (s) => s.rows },
        { header: 'coverage', value: (s) => s.coverage },
        { header: 'note', value: (s) => s.note },
      ],
      sources,
    ),
  );
}

/**
 * Data Health (V3-GOV-008). Which data is real, which is demo, and how clean it is — the governed
 * sources, the data-quality score with its band, and the itemised issues.
 */
export default function DataHealthPage() {
  const { data, isLoading, isError, error, refetch } = useDataHealth();

  return (
    <>
      <PageHeader
        title="Data Health"
        summary="Which sources are real, which are demo, and how clean the data behind every decision is."
        wireframed
        meta={[
          { label: 'Data source', value: 'Sample dataset (not live Oracle Fusion)' },
          { label: 'Last refreshed', value: data ? new Date(data.generatedAtUtc).toLocaleString() : '—' },
          { label: 'Implementation', value: 'Operational' },
        ]}
      />

      <div className="grid grid--stats">
        <StatCard
          label="Data-quality score"
          icon="verified"
          value={
            data ? (
              <span style={{ display: 'inline-flex', alignItems: 'baseline', gap: 8 }}>
                {data.score}
                <span className={`badge ${scoreBandClass[data.scoreBand] ?? 'badge'}`}>{data.scoreBand}</span>
              </span>
            ) : (
              '—'
            )
          }
          hint="0–100, higher is cleaner"
        />
        <StatCard label="Sales rows" value={data ? fmtInt(data.salesRows) : '—'} icon="table_rows" hint="Real Fusion sales history" />
        <StatCard label="Inventory rows" value={data ? fmtInt(data.invRows) : '—'} icon="inventory_2" hint="Real inventory snapshot" />
        <StatCard label="Sales coverage" value={data ? data.coverage : '—'} icon="calendar_month" hint="Observed month range" />
        <StatCard label="Models" value={data ? fmtInt(data.models) : '—'} icon="grid_view" hint="Distinct models in sales" />
        <StatCard label="Locations" value={data ? fmtInt(data.locations) : '—'} icon="location_on" hint="Distinct sales locations" />
      </div>

      <div style={{ height: 'var(--gap)' }} />

      <Card>
        <CardHeader
          title="Governed data sources"
          subtitle="Each source, its system of record, and an honest real / demo / blocked status."
          action={
            <button
              type="button"
              className="icon-btn"
              style={{ width: 'auto', padding: '6px 14px' }}
              onClick={() => data && exportSources(data.sources)}
              disabled={!data || data.sources.length === 0}
            >
              <Icon name="download" />
              Export CSV
            </button>
          }
        />

        {isLoading ? (
          <LoadingState label="Assessing every data source…" />
        ) : isError ? (
          <ErrorState
            title="Could not load data health"
            message={apiErrorMessage(error)}
            onRetry={() => void refetch()}
          />
        ) : !data || data.sources.length === 0 ? (
          <EmptyState title="No data sources" message="No governed data source is registered." icon="database" />
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table className="data-table">
              <thead>
                <tr>
                  <th>Source</th>
                  <th>System</th>
                  <th>Status</th>
                  <th className="num">Rows</th>
                  <th>Coverage</th>
                  <th>Note</th>
                </tr>
              </thead>
              <tbody>
                {data.sources.map((s) => (
                  <tr key={s.name}>
                    <td>{s.name}</td>
                    <td>{s.system}</td>
                    <td>
                      <SourceStatus source={s} />
                    </td>
                    <td className="num">{s.rows}</td>
                    <td>{s.coverage}</td>
                    <td>{s.note}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <div style={{ height: 'var(--gap)' }} />

      <Card>
        <CardHeader
          title="Data-quality issues"
          subtitle="The checks behind the score, each with its count and severity."
        />

        {isLoading ? (
          <LoadingState label="Running the data-quality checks…" />
        ) : isError ? (
          <ErrorState title="Could not load the issues" message={apiErrorMessage(error)} onRetry={() => void refetch()} />
        ) : !data || data.issues.length === 0 ? (
          <EmptyState title="No checks reported" message="The data-quality assessment returned no checks." icon="rule" />
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table className="data-table">
              <thead>
                <tr>
                  <th>Check</th>
                  <th className="num">Count</th>
                  <th>Severity</th>
                  <th>Note</th>
                </tr>
              </thead>
              <tbody>
                {data.issues.map((issue: DataQualityIssue) => (
                  <tr key={issue.id}>
                    <td>{issue.label}</td>
                    <td className="num">{issue.count}</td>
                    <td>
                      <span className={`badge ${severityClass[issue.severity] ?? 'badge'}`}>
                        {severityWord[issue.severity] ?? issue.severity}
                      </span>
                    </td>
                    <td>{issue.note}</td>
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
