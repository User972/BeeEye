import { useState } from 'react';
import { PageHeader } from '@/components/ui/PageHeader';
import { StatCard } from '@/components/ui/StatCard';
import { Card, CardHeader } from '@/components/ui/Card';
import { FilterSelect } from '@/components/ui/FilterSelect';
import { LoadingState, ErrorState, EmptyState } from '@/components/ui/states';
import { BarDistribution } from '@/components/charts/BarDistribution';
import { useConfigSummary, useConfigs, useConfigFilterOptions, type ConfigQuery } from '@/lib/api/config';
import { fmtInt, fmtNum, fmtSignPct, rotationClass } from '@/lib/format';

const PAGE_SIZE = 25;

export default function ConfigurationDemand() {
  const [model, setModel] = useState('');
  const [variant, setVariant] = useState('');
  const [colour, setColour] = useState('');
  const [rotation, setRotation] = useState('');
  const [page, setPage] = useState(1);
  const [sort, setSort] = useState('units');

  const filter: ConfigQuery = {
    ...(model ? { model: [model] } : {}),
    ...(variant ? { variant: [variant] } : {}),
    ...(colour ? { colour: [colour] } : {}),
    ...(rotation ? { rotation: [rotation] } : {}),
  };
  const options = useConfigFilterOptions();
  const summary = useConfigSummary(filter);
  const list = useConfigs({ ...filter, page, pageSize: PAGE_SIZE, sort });

  const reset = <T,>(setter: (v: T) => void) => (v: T) => {
    setter(v);
    setPage(1);
  };
  const sortBy = (key: string) => {
    setSort(key);
    setPage(1);
  };

  const s = summary.data?.summary;

  return (
    <>
      <PageHeader
        title="Configuration Demand"
        summary="Which model·variant·colour·interior combinations drive demand and which are becoming dead stock."
        useCase="UC3"
        meta={[
          { label: 'Configurations', value: summary.data ? `${summary.data.meta.filteredConfigurations} of ${summary.data.meta.totalConfigurations}` : '—' },
          { label: 'Data source', value: 'Sample dataset (not live Oracle Fusion)' },
        ]}
      />

      <div className="filter-bar">
        <FilterSelect label="Model" options={options.data?.models ?? []} value={model} onChange={reset(setModel)} />
        <FilterSelect label="Variant" options={options.data?.variants ?? []} value={variant} onChange={reset(setVariant)} />
        <FilterSelect label="Colour" options={options.data?.colours ?? []} value={colour} onChange={reset(setColour)} />
        <FilterSelect label="Rotation" options={options.data?.rotations ?? []} value={rotation} onChange={reset(setRotation)} />
      </div>

      {summary.isLoading ? (
        <LoadingState label="Analysing configurations…" />
      ) : summary.isError ? (
        <ErrorState title="API unavailable" message="Start the BeeEye API + PostgreSQL to load configuration demand." onRetry={() => void summary.refetch()} />
      ) : !s || s.configurations === 0 ? (
        <EmptyState title="No configurations match these filters" />
      ) : (
        <>
          <div className="grid grid--stats">
            <StatCard label="Configurations" value={fmtInt(s.configurations)} icon="grid_view" />
            <StatCard label="Fast-moving" value={fmtInt(s.fastCount)} icon="bolt" />
            <StatCard label="Dead stock" value={fmtInt(s.deadCount)} icon="do_not_disturb_on" hint="no recent sales" />
            <StatCard label="Decay alerts" value={fmtInt(s.decayAlerts)} icon="trending_down" />
            <StatCard label="Stockout-suspected" value={fmtInt(s.stockoutSuspected)} icon="report" hint="no stock to sell" />
          </div>

          <div style={{ height: 'var(--gap)' }} />

          <Card>
            <CardHeader title="Rotation mix" subtitle="Configurations by movement class" />
            <BarDistribution
              rows={s.byRotation.map((b) => ({
                key: b.key,
                label: b.key,
                value: b.configurations,
                band: b.key === 'Fast' ? 'Low' : b.key === 'Slow' ? 'Medium' : b.key === 'Dead' ? 'Critical' : 'New',
              }))}
              format={fmtInt}
              caption="Configurations by rotation class"
            />
          </Card>

          <div style={{ height: 'var(--gap)' }} />

          <Card>
            <CardHeader title="Configurations" subtitle="Sorted by demand — flags surface decay, availability gaps and cold starts" />
            {list.isLoading ? (
              <LoadingState />
            ) : list.isError || !list.data ? (
              <ErrorState onRetry={() => void list.refetch()} />
            ) : list.data.items.length === 0 ? (
              <EmptyState title="No configurations on this page" />
            ) : (
              <>
                <div className="grid-scroll">
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th>Configuration</th>
                        <SortHeader label="Units" active={sort === 'units'} onClick={() => sortBy('units')} />
                        <SortHeader label="Velocity" active={sort === 'velocity'} onClick={() => sortBy('velocity')} />
                        <SortHeader label="Decay" active={sort === 'decay'} dir="ascending" onClick={() => sortBy('decay')} />
                        <th>Rotation</th>
                        <SortHeader label="Stock" active={sort === 'stock'} onClick={() => sortBy('stock')} />
                        <th>Flags</th>
                      </tr>
                    </thead>
                    <tbody>
                      {list.data.items.map((c) => (
                        <tr key={`${c.model}|${c.variant}|${c.colour}|${c.interior}`}>
                          <td>{c.model} · {c.variant} · {c.colour}</td>
                          <td className="num">{fmtInt(c.totalUnits)}</td>
                          <td className="num">{fmtNum(c.recentVelocity, 1)}</td>
                          <td className="num">{fmtSignPct(c.decayPct)}</td>
                          <td><span className={`badge ${rotationClass(c.rotationClass)}`}>{c.rotationClass}</span></td>
                          <td className="num">{fmtInt(c.currentStock)}</td>
                          <td>
                            {c.decayAlert ? <span className="badge risk-high">decay</span> : null}{' '}
                            {c.stockoutSuspected ? <span className="badge risk-med">stockout?</span> : null}{' '}
                            {c.isColdStart ? <span className="badge">new</span> : null}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <div className="pager">
                  <span>Page {list.data.page} of {Math.max(1, Math.ceil(list.data.totalCount / list.data.pageSize))} · {list.data.totalCount} configs</span>
                  <button type="button" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Previous</button>
                  <button type="button" disabled={page >= Math.ceil(list.data.totalCount / list.data.pageSize)} onClick={() => setPage((p) => p + 1)}>Next</button>
                </div>
              </>
            )}
          </Card>
        </>
      )}
    </>
  );
}

function SortHeader({
  label,
  active,
  dir = 'descending',
  onClick,
}: {
  label: string;
  active: boolean;
  dir?: 'ascending' | 'descending';
  onClick: () => void;
}) {
  return (
    <th className="sortable num" onClick={onClick} aria-sort={active ? dir : undefined}>
      {label}
      <span aria-hidden> {active ? (dir === 'ascending' ? '↑' : '↓') : '↕'}</span>
    </th>
  );
}
