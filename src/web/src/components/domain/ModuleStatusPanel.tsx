import { Card, CardHeader } from '@/components/ui/Card';
import { LoadingState, ErrorState, EmptyState } from '@/components/ui/states';
import { useModuleInfo } from '@/lib/api/hooks';

/**
 * Live status of a bounded-context module, read from the API. Demonstrates the
 * per-screen loading / error / empty state handling required by the UX spec, and
 * proves the SPA ↔ API integration point without any live data wired yet.
 */
export function ModuleStatusPanel({ routePrefix }: { routePrefix: string }) {
  const { data, isLoading, isError, refetch } = useModuleInfo(routePrefix);

  return (
    <Card>
      <CardHeader title="Module service" subtitle={`Live status · GET /api/v1/${routePrefix}`} />
      {isLoading ? (
        <LoadingState label="Contacting module service…" />
      ) : isError ? (
        <ErrorState
          title="Module service unavailable"
          message="Start the BeeEye API host (dotnet run) to load live module data."
          onRetry={() => void refetch()}
        />
      ) : !data ? (
        <EmptyState title="No module data returned" />
      ) : (
        <dl style={{ display: 'grid', gridTemplateColumns: 'auto 1fr', gap: '8px 16px', margin: 0 }}>
          <dt style={{ color: 'var(--text-muted)' }}>Name</dt>
          <dd style={{ margin: 0, fontWeight: 600 }}>{data.name}</dd>
          <dt style={{ color: 'var(--text-muted)' }}>Status</dt>
          <dd style={{ margin: 0 }}>
            <span className="badge risk-med">{data.status}</span>
          </dd>
          <dt style={{ color: 'var(--text-muted)' }}>Description</dt>
          <dd style={{ margin: 0, color: 'var(--text-muted)' }}>{data.description}</dd>
        </dl>
      )}
    </Card>
  );
}
