import { useState } from 'react';
import { PageHeader } from '@/components/ui/PageHeader';
import { StatCard } from '@/components/ui/StatCard';
import { Card, CardHeader } from '@/components/ui/Card';
import { Drawer } from '@/components/ui/Drawer';
import { ExplainButton, ExplainabilityDrawer } from '@/components/domain/ExplainabilityDrawer';
import { useExplainabilityDrawer } from '@/components/domain/useExplainabilityDrawer';
import { FilterSelect } from '@/components/ui/FilterSelect';
import { LoadingState, ErrorState, EmptyState } from '@/components/ui/states';
import { BarDistribution } from '@/components/charts/BarDistribution';
import { SyntheticBanner } from '@/components/domain/SyntheticBanner';
import { DecisionFooter } from '@/components/domain/DecisionFooter';
import {
  useAfterSalesSummary,
  useAfterSalesByModel,
  useAfterSalesModel,
  useAfterSalesFilterOptions,
  type AfterSalesQuery,
  type ModelServiceIntensity,
} from '@/lib/api/aftersales';
import { fmtInt, fmtNum, fmtPct, intensityClass, reliabilityClass } from '@/lib/format';

const PAGE_SIZE = 20;

export default function AfterSalesPage() {
  const [model, setModel] = useState('');
  const [highOnly, setHighOnly] = useState(false);
  const [page, setPage] = useState(1);
  const [sort, setSort] = useState('intensity');
  const [selected, setSelected] = useState<string | null>(null);

  const options = useAfterSalesFilterOptions();
  const summary = useAfterSalesSummary();
  const query: AfterSalesQuery = {
    ...(model ? { model: [model] } : {}),
    ...(highOnly ? { highOnly: true } : {}),
    page,
    pageSize: PAGE_SIZE,
    sort,
  };
  const byModel = useAfterSalesByModel(query);
  const detail = useAfterSalesModel(selected);
  const explain = useExplainabilityDrawer();

  const reset = <T,>(setter: (v: T) => void) => (v: T) => {
    setter(v);
    setPage(1);
  };

  const s = summary.data?.summary;

  return (
    <>
      <PageHeader
        title="Sales ↔ Service Correlation"
        summary="How much workshop and parts demand each vehicle sale generates — service intensity by model, mileage, age and service type."
        useCase="UC6"
        wireframed
        meta={[
          { label: 'Models tracked', value: s ? fmtInt(s.modelsTracked) : '—' },
          { label: 'Months of history', value: s ? fmtInt(s.monthsOfHistory) : '—' },
          { label: 'Data source', value: 'Synthetic demo (not Oracle Fusion)' },
        ]}
      />

      <SyntheticBanner />

      <div className="filter-bar">
        <FilterSelect label="Model" options={options.data?.models ?? []} value={model} onChange={reset(setModel)} />
        <label className="filter-select">
          <span>High-intensity only</span>
          <input type="checkbox" checked={highOnly} onChange={(e) => reset(setHighOnly)(e.target.checked)} aria-label="Show high-intensity models only" />
        </label>
      </div>

      {summary.isLoading ? (
        <LoadingState label="Computing service intensity…" />
      ) : summary.isError ? (
        <ErrorState
          title="API unavailable"
          message="Start the BeeEye API + PostgreSQL (docker compose up) to load after-sales correlation."
          onRetry={() => void summary.refetch()}
        />
      ) : !s || s.modelsTracked === 0 ? (
        <EmptyState title="No after-sales data available" />
      ) : (
        <>
          <div className="grid grid--stats">
            <StatCard label="Models tracked" value={fmtInt(s.modelsTracked)} icon="directions_car" />
            <StatCard label="Vehicles in operation" value={fmtInt(s.totalVehiclesInOperation)} icon="groups" hint="fleet size" />
            <StatCard
              label="Avg. service-intensity"
              value={fmtNum(s.averageIntensityIndex, 2)}
              icon="build"
              hint="fleet mean = 1.00"
            />
            <StatCard label="High-intensity models" value={fmtInt(s.highIntensityModels)} icon="priority_high" hint="≥ 1.25 × fleet mean" />
            <StatCard
              label="Data coverage"
              value={s.overallCoverageRate === null ? '—' : fmtPct(s.overallCoverageRate * 100, 0)}
              icon="fact_check"
              hint="vehicles with ≥1 service event"
            />
            <StatCard label="Service events" value={fmtInt(s.totalEvents)} icon="construction" />
          </div>

          <div style={{ height: 'var(--gap)' }} />

          <Card>
            <CardHeader title="Service-intensity index by model" subtitle="Events per vehicle-in-operation, normalised so the fleet mean is 1.00" />
            {byModel.isLoading ? (
              <LoadingState />
            ) : byModel.isError || !byModel.data ? (
              <ErrorState onRetry={() => void byModel.refetch()} />
            ) : (
              <BarDistribution
                rows={byModel.data.page.items.map((m) => ({
                  key: m.model,
                  label: m.model,
                  value: m.intensityIndex ?? 0,
                  band: m.highIntensity ? 'High' : (m.intensityIndex ?? 0) >= 1 ? 'Medium' : 'Low',
                }))}
                format={(v) => fmtNum(v, 2)}
                caption="Service-intensity index by model"
              />
            )}
          </Card>

          <div style={{ height: 'var(--gap)' }} />

          <Card>
            <CardHeader title="Models" subtitle="Click a row for mileage-band, time-since-sale and service-type breakdowns" />
            {byModel.isLoading ? (
              <LoadingState />
            ) : byModel.isError || !byModel.data ? (
              <ErrorState onRetry={() => void byModel.refetch()} />
            ) : byModel.data.page.items.length === 0 ? (
              <EmptyState title="No models match this filter" />
            ) : (
              <>
                <div className="grid-scroll">
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th>Model</th>
                        <SortHeader label="Intensity" active={sort === 'intensity'} onClick={() => { setSort('intensity'); setPage(1); }} />
                        <SortHeader label="Events" active={sort === 'events'} onClick={() => { setSort('events'); setPage(1); }} />
                        <th className="num">Events/veh</th>
                        <SortHeader label="Labour/veh" active={sort === 'labor'} onClick={() => { setSort('labor'); setPage(1); }} />
                        <SortHeader label="Coverage" active={sort === 'coverage'} onClick={() => { setSort('coverage'); setPage(1); }} />
                        <th>Reliability</th>
                        <th>Flag</th>
                      </tr>
                    </thead>
                    <tbody>
                      {byModel.data.page.items.map((m) => (
                        <tr key={m.model} className="sortable" onClick={() => setSelected(m.model)}>
                          <td>{m.model}</td>
                          <td className="num"><span className={`badge ${intensityClass(m.intensityIndex, m.highIntensity)}`}>{fmtNum(m.intensityIndex, 2)}</span></td>
                          <td className="num">{fmtInt(m.totalEvents)}</td>
                          <td className="num">{fmtNum(m.eventsPerVehicle, 2)}</td>
                          <td className="num">{fmtNum(m.laborHoursPerVehicle, 2)}</td>
                          <td className="num">{m.coverageRate === null ? '—' : fmtPct(m.coverageRate * 100, 0)}</td>
                          <td><span className={`badge ${reliabilityClass(m.reliabilityTier)}`}>{m.reliabilityTier}</span></td>
                          <td>{m.highIntensity ? <span className="badge risk-high">high-service</span> : null}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <div className="pager">
                  <span>Page {byModel.data.page.page} of {Math.max(1, Math.ceil(byModel.data.page.totalCount / byModel.data.page.pageSize))} · {byModel.data.page.totalCount} models</span>
                  <button type="button" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Previous</button>
                  <button
                    type="button"
                    disabled={page >= Math.ceil(byModel.data.page.totalCount / byModel.data.page.pageSize)}
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

      <Drawer
        open={selected !== null}
        title="Model service intensity"
        onClose={() => setSelected(null)}
        footer={detail.data ? <DecisionFooter subjectRef={detail.data.model.model} /> : null}
      >
        {detail.isLoading ? (
          <LoadingState />
        ) : detail.isError || !detail.data ? (
          <ErrorState title="Could not load model" />
        ) : (
          <>
            <ExplainButton
              kind="service-model"
              label={detail.data.model.model}
              onClick={() => explain.open({ kind: 'service-model', ref: detail.data.model.model })}
            />
            <ModelDetail data={detail.data.model} />
          </>
        )}
      </Drawer>

      <ExplainabilityDrawer subject={explain.subject} onClose={explain.close} />
    </>
  );
}

function ModelDetail({ data }: { data: ModelServiceIntensity }) {
  return (
    <>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <div style={{ fontWeight: 700, fontSize: 16 }}>{data.model}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 12.5 }}>
            {fmtInt(data.vehiclesInOperation)} vehicles · {fmtInt(data.totalEvents)} service events
          </div>
        </div>
        <span className={`badge ${intensityClass(data.intensityIndex, data.highIntensity)}`}>
          index {fmtNum(data.intensityIndex, 2)}
        </span>
      </div>

      <dl style={{ display: 'grid', gridTemplateColumns: 'auto 1fr', gap: '6px 14px', margin: 0, fontSize: 12.5 }}>
        <dt style={{ color: 'var(--text-muted)' }}>Events / vehicle</dt><dd style={{ margin: 0 }}>{fmtNum(data.eventsPerVehicle, 2)}</dd>
        <dt style={{ color: 'var(--text-muted)' }}>Labour-hours / vehicle</dt><dd style={{ margin: 0 }}>{fmtNum(data.laborHoursPerVehicle, 2)}</dd>
        <dt style={{ color: 'var(--text-muted)' }}>Coverage</dt>
        <dd style={{ margin: 0 }}>
          {data.coverage.coverageRate === null ? '—' : fmtPct(data.coverage.coverageRate * 100, 0)} of vehicles ·
          {' '}{fmtInt(data.coverage.monthsOfHistory)} months ·
          {' '}<span className={`badge ${reliabilityClass(data.coverage.reliabilityTier)}`}>{data.coverage.reliabilityTier}</span>
        </dd>
      </dl>

      <div>
        <div className="card__title" style={{ marginBottom: 8 }}>Service type mix (planned vs unplanned kept separate)</div>
        <BarDistribution
          rows={data.byServiceType.map((t) => ({
            key: t.serviceType,
            label: t.serviceType,
            value: t.events,
            band: t.serviceType === 'Routine' ? 'Low' : t.serviceType === 'Repair' ? 'Medium' : t.serviceType === 'Warranty' ? 'High' : 'Critical',
          }))}
          format={fmtInt}
          caption="Service events by type"
        />
      </div>

      <div>
        <div className="card__title" style={{ marginBottom: 8 }}>By mileage band</div>
        <BarDistribution
          rows={data.byMileageBand.map((b) => ({ key: b.band, label: b.band, value: b.events }))}
          format={fmtInt}
          caption="Service events by mileage band"
        />
      </div>

      <div>
        <div className="card__title" style={{ marginBottom: 8 }}>By time since sale</div>
        <BarDistribution
          rows={data.byTimeSinceSale.map((b) => ({ key: b.band, label: b.band, value: b.events }))}
          format={fmtInt}
          caption="Service events by time since sale"
        />
      </div>

      <Card className="stat-card">
        <div className="card__title" style={{ marginBottom: 4 }}>Sales → service association</div>
        <p style={{ fontSize: 12.5, color: 'var(--text-muted)', margin: 0 }}>{data.correlation.interpretation}</p>
        {data.correlation.best !== null ? (
          <p style={{ fontSize: 12, margin: '6px 0 0' }}>
            <b>Best correlation:</b> {fmtNum(data.correlation.best, 2)} at a {data.correlation.bestLagMonths}-month lag.
          </p>
        ) : null}
      </Card>
    </>
  );
}

function SortHeader({ label, active, onClick }: { label: string; active: boolean; onClick: () => void }) {
  return (
    <th className="sortable num" onClick={onClick} aria-sort={active ? 'descending' : undefined}>
      {label}
      <span aria-hidden> {active ? '↓' : '↕'}</span>
    </th>
  );
}
