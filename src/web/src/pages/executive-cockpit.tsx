import { PageHeader } from '@/components/ui/PageHeader';
import { StatCard } from '@/components/ui/StatCard';
import { Card, CardHeader } from '@/components/ui/Card';
import { LoadingState, ErrorState, EmptyState } from '@/components/ui/states';
import { usePlatformModules } from '@/lib/api/hooks';

/** UC8 — Executive Decision Cockpit. Landing screen; aggregates modules today,
 *  and (in a later increment) prioritised material exceptions across the platform. */
export default function ExecutiveCockpit() {
  const { data, isLoading, isError, refetch } = usePlatformModules();

  return (
    <>
      <PageHeader
        title="Executive Cockpit"
        summary="Material exceptions from every module, prioritised for decision this month."
        useCase="UC8"
        wireframed
        meta={[
          { label: 'Data source', value: 'Sample dataset (not live Oracle Fusion)' },
          { label: 'Last refreshed', value: '—' },
          { label: 'Implementation', value: 'Scaffolded' },
        ]}
      />

      <div className="grid grid--stats">
        <StatCard label="Modules online" value={data ? data.length : '—'} icon="hub" />
        <StatCard label="Open decisions" value="—" icon="gavel" hint="Wired with the decision workflow" />
        <StatCard label="Financial exposure" value="SAR —" icon="payments" hint="Aggregated from modules" />
        <StatCard label="Critical risks" value="—" icon="warning" hint="From inventory & procurement" />
      </div>

      <div style={{ height: 'var(--gap)' }} />

      <Card>
        <CardHeader title="Platform modules" subtitle="Bounded contexts mounted by the API host" />
        {isLoading ? (
          <LoadingState label="Loading modules…" />
        ) : isError ? (
          <ErrorState
            title="API unavailable"
            message="Start the BeeEye API host (dotnet run) to load platform modules."
            onRetry={() => void refetch()}
          />
        ) : !data || data.length === 0 ? (
          <EmptyState title="No modules mounted" />
        ) : (
          <div className="grid grid--cards">
            {data.map((module) => (
              <div key={module.routePrefix} className="card" style={{ boxShadow: 'none', background: 'var(--surface-2)' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8 }}>
                  <strong style={{ fontSize: 13.5 }}>{module.name}</strong>
                  <span className="badge">{module.status}</span>
                </div>
                <p style={{ color: 'var(--text-muted)', fontSize: 12, marginTop: 6, marginBottom: 0 }}>
                  {module.description}
                </p>
              </div>
            ))}
          </div>
        )}
      </Card>
    </>
  );
}
