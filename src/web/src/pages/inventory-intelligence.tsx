import { useState } from 'react';
import { PageHeader } from '@/components/ui/PageHeader';
import { StatCard } from '@/components/ui/StatCard';
import { Card, CardHeader } from '@/components/ui/Card';
import { Drawer } from '@/components/ui/Drawer';
import { FilterSelect } from '@/components/ui/FilterSelect';
import { LoadingState, ErrorState, EmptyState } from '@/components/ui/states';
import { BarDistribution } from '@/components/charts/BarDistribution';
import { InventoryTable } from '@/components/domain/InventoryTable';
import {
  useInventorySummary,
  useInventoryItems,
  useInventoryItem,
  useInventoryFilterOptions,
  type InventoryQuery,
} from '@/lib/api/inventory';
import { fmtInt, fmtNum, fmtSar, fmtSignPct, riskClass } from '@/lib/format';

const PAGE_SIZE = 25;

export default function InventoryIntelligence() {
  const [model, setModel] = useState('');
  const [variant, setVariant] = useState('');
  const [location, setLocation] = useState('');
  const [riskBand, setRiskBand] = useState('');
  const [page, setPage] = useState(1);
  const [sort, setSort] = useState('risk');
  const [selected, setSelected] = useState<string | null>(null);

  const filter: InventoryQuery = {
    ...(model ? { model: [model] } : {}),
    ...(variant ? { variant: [variant] } : {}),
    ...(location ? { location: [location] } : {}),
    ...(riskBand ? { riskBand: [riskBand] } : {}),
  };

  const options = useInventoryFilterOptions();
  const summary = useInventorySummary(filter);
  const items = useInventoryItems({ ...filter, page, pageSize: PAGE_SIZE, sort });
  const detail = useInventoryItem(selected);

  const reset = <T,>(setter: (v: T) => void) => (v: T) => {
    setter(v);
    setPage(1);
  };

  const meta = summary.data?.meta;

  return (
    <>
      <PageHeader
        title="Inventory Intelligence"
        summary="Aging, overstock risk and explainable risk scoring across the vehicle stock."
        useCase="UC5"
        wireframed
        meta={[
          { label: 'Analysis date', value: meta?.analysisDate ?? '30 Jun 2026' },
          { label: 'Units', value: meta ? `${meta.filteredUnits} of ${meta.totalUnits}` : '—' },
          { label: 'Data source', value: 'Sample dataset (not live Oracle Fusion)' },
        ]}
      />

      <div className="filter-bar">
        <FilterSelect label="Model" options={options.data?.models ?? []} value={model} onChange={reset(setModel)} />
        <FilterSelect label="Variant" options={options.data?.variants ?? []} value={variant} onChange={reset(setVariant)} />
        <FilterSelect label="Location" options={options.data?.locations ?? []} value={location} onChange={reset(setLocation)} />
        <FilterSelect label="Risk band" options={options.data?.riskBands ?? []} value={riskBand} onChange={reset(setRiskBand)} />
      </div>

      {summary.isLoading ? (
        <LoadingState label="Computing risk…" />
      ) : summary.isError ? (
        <ErrorState
          title="API unavailable"
          message="Start the BeeEye API + PostgreSQL (docker compose up) to load inventory risk."
          onRetry={() => void summary.refetch()}
        />
      ) : !summary.data || summary.data.summary.count === 0 ? (
        <EmptyState title="No inventory matches these filters" />
      ) : (
        <>
          <div className="grid grid--stats">
            <StatCard label="Inventory value" value={fmtSar(summary.data.summary.value)} icon="payments" />
            <StatCard label="Units" value={fmtInt(summary.data.summary.count)} icon="inventory_2" />
            <StatCard label="Avg. inventory age" value={`${fmtNum(summary.data.summary.averageInventoryAge, 0)} d`} icon="schedule" />
            <StatCard
              label="High + critical value"
              value={fmtSar(summary.data.summary.highRiskValue)}
              icon="warning"
              hint={`${summary.data.summary.criticalCount} critical · ${summary.data.summary.highCount} high`}
            />
            <StatCard label="Daily holding cost" value={fmtSar(summary.data.summary.dailyHoldingCost, { perDay: true })} icon="local_atm" />
          </div>

          <div style={{ height: 'var(--gap)' }} />

          <div className="grid grid--cards">
            <Card>
              <CardHeader title="Risk distribution" subtitle="Units by risk band" />
              <BarDistribution
                rows={summary.data.summary.byRisk.map((b) => ({ key: b.key, label: b.key, value: b.units, band: b.key }))}
                format={fmtInt}
                caption="Units by risk band"
              />
            </Card>
            <Card>
              <CardHeader title="Aging distribution" subtitle="Units by aging band" />
              <BarDistribution
                rows={summary.data.summary.byAging.map((b) => ({ key: b.key, label: b.key, value: b.units, band: b.key }))}
                format={fmtInt}
                caption="Units by aging band"
              />
            </Card>
          </div>

          <div style={{ height: 'var(--gap)' }} />

          <Card>
            <CardHeader title="Inventory units" subtitle="Click a row for the full risk breakdown and recommendation" />
            {items.isLoading ? (
              <LoadingState />
            ) : items.isError ? (
              <ErrorState onRetry={() => void items.refetch()} />
            ) : !items.data || items.data.items.length === 0 ? (
              <EmptyState title="No units on this page" />
            ) : (
              <>
                <InventoryTable rows={items.data.items} onSelect={setSelected} onSortChange={reset(setSort)} />
                <div className="pager">
                  <span>
                    Page {items.data.page} of {Math.max(1, Math.ceil(items.data.totalCount / items.data.pageSize))} · {items.data.totalCount} units
                  </span>
                  <button type="button" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Previous</button>
                  <button
                    type="button"
                    disabled={page >= Math.ceil(items.data.totalCount / items.data.pageSize)}
                    onClick={() => setPage((p) => p + 1)}
                  >
                    Next
                  </button>
                </div>
              </>
            )}
          </Card>
        </>
      )}

      <Drawer open={selected !== null} title="Inventory unit" onClose={() => setSelected(null)}>
        {detail.isLoading ? (
          <LoadingState />
        ) : detail.isError || !detail.data ? (
          <ErrorState title="Could not load unit" />
        ) : (
          <UnitDetail data={detail.data} />
        )}
      </Drawer>
    </>
  );
}

function UnitDetail({ data }: { data: NonNullable<ReturnType<typeof useInventoryItem>['data']> }) {
  const maxPoints = Math.max(1, ...data.factors.map((f) => f.points));
  const bandClass = riskClass(data.riskBand);
  return (
    <>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <div style={{ fontWeight: 700, fontSize: 16 }}>{data.model} {data.variant}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 12.5 }}>{data.stockId} · {data.location} · {data.colour}</div>
        </div>
        <span className={`badge ${bandClass}`}>{data.riskScore} · {data.riskBand}</span>
      </div>

      <dl style={{ display: 'grid', gridTemplateColumns: 'auto 1fr', gap: '6px 14px', margin: 0, fontSize: 12.5 }}>
        <dt style={{ color: 'var(--text-muted)' }}>Inventory age</dt><dd style={{ margin: 0 }}>{fmtInt(data.inventoryAgeDays)} days ({data.agingBand})</dd>
        <dt style={{ color: 'var(--text-muted)' }}>Stock cover</dt><dd style={{ margin: 0 }}>{data.stockCover >= 999 ? '∞' : `${fmtNum(data.stockCover, 1)} months`}</dd>
        <dt style={{ color: 'var(--text-muted)' }}>Demand</dt><dd style={{ margin: 0 }}>{fmtNum(data.velocity, 1)}/mo · {data.demandBasis} ({data.demandConfidence})</dd>
        <dt style={{ color: 'var(--text-muted)' }}>Trend</dt><dd style={{ margin: 0 }}>{data.trendDirection} ({fmtSignPct(data.trendChangePct)})</dd>
        <dt style={{ color: 'var(--text-muted)' }}>Holding accrued</dt><dd style={{ margin: 0 }}>{fmtSar(data.accumulatedHoldingCost)}</dd>
        <dt style={{ color: 'var(--text-muted)' }}>Purchase price</dt><dd style={{ margin: 0 }}>{fmtSar(data.purchasePrice)}</dd>
      </dl>

      <div>
        <div className="card__title" style={{ marginBottom: 8 }}>Risk breakdown</div>
        <div style={{ display: 'grid', gap: 12 }}>
          {data.factors.map((f) => (
            <div className="factor" key={f.key}>
              <div className="factor__head">
                <span>{f.label}</span>
                <span className="tabular">{fmtNum(f.points, 1)} pts</span>
              </div>
              <div className="factor__bar"><div className="factor__fill" style={{ width: `${(f.points / maxPoints) * 100}%` }} /></div>
              <div className="factor__detail">{f.detail}</div>
            </div>
          ))}
        </div>
      </div>

      <Card className="stat-card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <strong>{data.recommendation.action}</strong>
          <span className="badge">{data.recommendation.confidence} confidence</span>
        </div>
        <p style={{ fontSize: 12.5, color: 'var(--text-muted)', margin: '6px 0' }}>{data.recommendation.why}</p>
        <ul style={{ margin: 0, paddingLeft: 18, fontSize: 12 }}>
          {data.recommendation.evidence.map((e) => <li key={e}>{e}</li>)}
        </ul>
        <p style={{ fontSize: 12, margin: '8px 0 0' }}><b>Expected outcome:</b> {data.recommendation.outcome}</p>
        {data.recommendation.assumptions.length > 0 ? (
          <p style={{ fontSize: 11.5, color: 'var(--text-faint)', margin: '6px 0 0' }}>
            Assumptions: {data.recommendation.assumptions.join(' ')}
          </p>
        ) : null}
      </Card>
    </>
  );
}
