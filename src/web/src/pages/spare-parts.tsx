import { useState } from 'react';
import { PageHeader } from '@/components/ui/PageHeader';
import { StatCard } from '@/components/ui/StatCard';
import { Card, CardHeader } from '@/components/ui/Card';
import { Drawer } from '@/components/ui/Drawer';
import { FilterSelect } from '@/components/ui/FilterSelect';
import { ScenarioSelect } from '@/components/ui/ScenarioSelect';
import { LoadingState, ErrorState, EmptyState } from '@/components/ui/states';
import { BarDistribution } from '@/components/charts/BarDistribution';
import { SyntheticBanner } from '@/components/domain/SyntheticBanner';
import { DecisionFooter } from '@/components/domain/DecisionFooter';
import {
  useSparePartsSummary,
  useSpareParts,
  useSparePart,
  useSparePartsFilterOptions,
  type PartDetailResponse,
  type SparePartsListQuery,
  type UsagePoint,
} from '@/lib/api/spareparts';
import { demandClassBadge, fmtInt, fmtNum, riskWordClass } from '@/lib/format';

const PAGE_SIZE = 25;

function fmtRange(lo: number | null, hi: number | null): string {
  return lo === null || hi === null ? 'insufficient data' : `${fmtInt(lo)}–${fmtInt(hi)}`;
}

export default function SparePartsPage() {
  const [service, setService] = useState('0.95');
  const [review, setReview] = useState('1');
  const [category, setCategory] = useState('');
  const [lowDataOnly, setLowDataOnly] = useState(false);
  const [atRiskOnly, setAtRiskOnly] = useState(false);
  const [page, setPage] = useState(1);
  const [sort, setSort] = useState('demand');
  const [selected, setSelected] = useState<string | null>(null);

  const scenario = { serviceLevel: Number(service), reviewPeriodMonths: Number(review) };
  const options = useSparePartsFilterOptions();
  const summary = useSparePartsSummary(scenario);
  const listQuery: SparePartsListQuery = {
    ...scenario,
    ...(category ? { category: [category] } : {}),
    ...(lowDataOnly ? { lowDataOnly: true } : {}),
    ...(atRiskOnly ? { atRiskOnly: true } : {}),
    page,
    pageSize: PAGE_SIZE,
    sort,
  };
  const list = useSpareParts(listQuery);
  const detail = useSparePart(selected, scenario);

  const reset = <T,>(setter: (v: T) => void) => (v: T) => {
    setter(v);
    setPage(1);
  };

  const s = summary.data?.summary;

  return (
    <>
      <PageHeader
        title="Spare Parts Prediction"
        summary="Intermittent spare-parts demand and stocking ranges per workshop — Croston/SBA/TSB where demand is lumpy, with honest confidence and low-data flags."
        useCase="UC7"
        meta={[
          { label: 'Stocking grain', value: 'part × location' },
          { label: 'Service level', value: `${Math.round(Number(service) * 100)}%` },
          { label: 'Data source', value: 'Synthetic demo (not Oracle Fusion)' },
        ]}
      />

      <SyntheticBanner />

      <div className="filter-bar">
        <ScenarioSelect
          label="Service level"
          value={service}
          onChange={reset(setService)}
          options={[{ value: '0.9', label: '90%' }, { value: '0.95', label: '95%' }, { value: '0.99', label: '99%' }]}
        />
        <ScenarioSelect
          label="Review period (months)"
          value={review}
          onChange={reset(setReview)}
          options={[{ value: '1', label: '1' }, { value: '2', label: '2' }, { value: '3', label: '3' }]}
        />
        <FilterSelect label="Category" options={options.data?.categories ?? []} value={category} onChange={reset(setCategory)} />
        <label className="filter-select">
          <span>Low-data only</span>
          <input type="checkbox" checked={lowDataOnly} onChange={(e) => reset(setLowDataOnly)(e.target.checked)} aria-label="Show low-data parts only" />
        </label>
        <label className="filter-select">
          <span>At-risk only</span>
          <input type="checkbox" checked={atRiskOnly} onChange={(e) => reset(setAtRiskOnly)(e.target.checked)} aria-label="Show at-risk parts only" />
        </label>
      </div>

      {summary.isLoading ? (
        <LoadingState label="Forecasting parts demand…" />
      ) : summary.isError ? (
        <ErrorState
          title="API unavailable"
          message="Start the BeeEye API + PostgreSQL (docker compose up) to load spare-parts demand."
          onRetry={() => void summary.refetch()}
        />
      ) : !s || s.stockingPoints === 0 ? (
        <EmptyState title="No spare-parts data available" />
      ) : (
        <>
          <div className="grid grid--stats">
            <StatCard label="Parts" value={fmtInt(s.distinctParts)} icon="settings" hint={`${fmtInt(s.stockingPoints)} part×location`} />
            <StatCard label="Predicted demand" value={`${fmtInt(s.predictedMonthlyDemandTotal)}/mo`} icon="trending_up" hint="units across all locations" />
            <StatCard label="Low-data points" value={fmtInt(s.lowDataPoints)} icon="help" hint="flagged, not fabricated" />
            <StatCard label="At-risk points" value={fmtInt(s.atRiskPoints)} icon="warning" hint="high stockout risk" />
          </div>

          <div style={{ height: 'var(--gap)' }} />

          <Card>
            <CardHeader title="Demand-class mix" subtitle="Part×location series by SBC class — intermittent/lumpy series drive the Croston family" />
            <BarDistribution
              rows={s.byDemandClass.map((c) => ({
                key: c.demandClass,
                label: c.demandClass,
                value: c.count,
                band:
                  c.demandClass === 'Smooth' ? 'Low'
                  : c.demandClass === 'Lumpy' || c.demandClass === 'Obsolescent' ? 'High'
                  : c.demandClass === 'InsufficientData' ? 'New'
                  : 'Medium',
              }))}
              format={fmtInt}
              caption="Part×location series by demand class"
            />
          </Card>

          <div style={{ height: 'var(--gap)' }} />

          <Card>
            <CardHeader title="Parts demand & stocking" subtitle="Per part × location. Click a row for method comparison, forecast range and supersession." />
            {list.isLoading ? (
              <LoadingState />
            ) : list.isError || !list.data ? (
              <ErrorState onRetry={() => void list.refetch()} />
            ) : list.data.page.items.length === 0 ? (
              <EmptyState title="No parts match these filters" />
            ) : (
              <>
                <div className="grid-scroll">
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th>Part</th>
                        <th>Location</th>
                        <th>Class</th>
                        <th>Method</th>
                        <SortHeader label="Demand/mo" active={sort === 'demand'} onClick={() => { setSort('demand'); setPage(1); }} />
                        <th className="num">Range</th>
                        <th className="num">Stock</th>
                        <th className="num">Lead (d)</th>
                        <SortHeader label="Risk" active={sort === 'risk'} onClick={() => { setSort('risk'); setPage(1); }} />
                        <th>Flag</th>
                      </tr>
                    </thead>
                    <tbody>
                      {list.data.page.items.map((r) => (
                        <tr key={`${r.partNumber}|${r.location}`} className="sortable" onClick={() => setSelected(r.partNumber)}>
                          <td>{r.partNumber}<div style={{ color: 'var(--text-faint)', fontSize: 11 }}>{r.name}</div></td>
                          <td>{r.location}</td>
                          <td><span className={`badge ${demandClassBadge(r.demandClass)}`}>{r.demandClass}</span></td>
                          <td>{r.method}</td>
                          <td className="num">{fmtNum(r.predictedMonthlyDemand, 2)}</td>
                          <td className="num">{fmtRange(r.stockingRangeLow, r.stockingRangeHigh)}</td>
                          <td className="num">{fmtInt(r.currentStock)}{r.inboundStock > 0 ? ` +${fmtInt(r.inboundStock)}` : ''}</td>
                          <td className="num">{fmtInt(r.leadTimeDays)}</td>
                          <td>{r.insufficientData ? '—' : <span className={`badge ${riskWordClass(r.stockoutRisk)}`}>{r.stockoutRisk}</span>}</td>
                          <td>{r.insufficientData ? <span className="badge">low data</span> : r.holdingRisk === 'Overstock' ? <span className="badge risk-med">overstock</span> : null}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <div className="pager">
                  <span>Page {list.data.page.page} of {Math.max(1, Math.ceil(list.data.page.totalCount / list.data.page.pageSize))} · {list.data.page.totalCount} part×location</span>
                  <button type="button" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Previous</button>
                  <button
                    type="button"
                    disabled={page >= Math.ceil(list.data.page.totalCount / list.data.page.pageSize)}
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
        title="Part demand & stocking"
        onClose={() => setSelected(null)}
        footer={detail.data ? <DecisionFooter subjectRef={detail.data.national.name} /> : null}
      >
        {detail.isLoading ? (
          <LoadingState />
        ) : detail.isError || !detail.data ? (
          <ErrorState title="Could not load part" />
        ) : (
          <PartDetail data={detail.data} />
        )}
      </Drawer>
    </>
  );
}

function PartDetail({ data }: { data: PartDetailResponse }) {
  const n = data.national;
  return (
    <>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <div style={{ fontWeight: 700, fontSize: 16 }}>{n.partNumber}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 12.5 }}>{n.name} · {n.category}</div>
        </div>
        <span className={`badge ${demandClassBadge(n.class)}`}>{n.class}</span>
      </div>

      <dl style={{ display: 'grid', gridTemplateColumns: 'auto 1fr', gap: '6px 14px', margin: 0, fontSize: 12.5 }}>
        <dt style={{ color: 'var(--text-muted)' }}>National demand</dt><dd style={{ margin: 0 }}>{fmtNum(n.predictedMonthlyDemand, 2)} /mo · method {n.method} ({n.confidence})</dd>
        <dt style={{ color: 'var(--text-muted)' }}>Intermittency</dt><dd style={{ margin: 0 }}>ADI {fmtNum(n.adi, 2)} · CV² {fmtNum(n.cv2, 2)} · {n.nonZeroPeriods}/{n.periods} months with demand</dd>
        <dt style={{ color: 'var(--text-muted)' }}>Lead time</dt><dd style={{ margin: 0 }}>{fmtNum(n.leadTimeMonths, 1)} months</dd>
        <dt style={{ color: 'var(--text-muted)' }}>Compatible models</dt><dd style={{ margin: 0 }}>{data.compatibleModels.join(', ') || '—'}</dd>
      </dl>

      <div>
        <div className="card__title" style={{ marginBottom: 8 }}>Usage history (national, zeros shown)</div>
        <UsageSparkline points={data.usageHistory} />
      </div>

      <div>
        <div className="card__title" style={{ marginBottom: 8 }}>Method comparison (per-period rate)</div>
        <div className="grid-scroll">
          <table className="data-table">
            <thead><tr><th>SES</th><th>Croston</th><th>SBA</th><th>TSB</th><th>Chosen</th></tr></thead>
            <tbody>
              <tr>
                <td className="num">{fmtNum(n.comparison.ses, 2)}</td>
                <td className="num">{fmtNum(n.comparison.croston, 2)}</td>
                <td className="num">{fmtNum(n.comparison.sba, 2)}</td>
                <td className="num">{fmtNum(n.comparison.tsb, 2)}</td>
                <td><span className="badge">{n.method}</span></td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      {data.rolledUpSupersessions.length > 0 || data.supersededByPartNumber ? (
        <Card className="stat-card">
          <div className="card__title" style={{ marginBottom: 4 }}>Supersession</div>
          {data.supersededByPartNumber ? (
            <p style={{ fontSize: 12.5, margin: 0 }}>Superseded by <b>{data.supersededByPartNumber}</b>.</p>
          ) : null}
          {data.rolledUpSupersessions.map((s) => (
            <p key={`${s.oldPartNumber}->${s.newPartNumber}`} style={{ fontSize: 12.5, margin: '4px 0 0' }}>
              <b>{s.oldPartNumber}</b> → <b>{s.newPartNumber}</b> from {s.effectiveDate} — history rolled onto the successor.
            </p>
          ))}
        </Card>
      ) : null}

      <div>
        <div className="card__title" style={{ marginBottom: 8 }}>By location</div>
        <div className="grid-scroll">
          <table className="data-table">
            <thead><tr><th>Location</th><th>Class</th><th>Method</th><th className="num">Demand/mo</th><th className="num">Range</th><th>Risk</th></tr></thead>
            <tbody>
              {data.byLocation.map((r) => (
                <tr key={r.location}>
                  <td>{r.location}</td>
                  <td><span className={`badge ${demandClassBadge(r.demandClass)}`}>{r.demandClass}</span></td>
                  <td>{r.method}</td>
                  <td className="num">{fmtNum(r.predictedMonthlyDemand, 2)}</td>
                  <td className="num">{fmtRange(r.stockingRangeLow, r.stockingRangeHigh)}</td>
                  <td>{r.insufficientData ? <span className="badge">low data</span> : <span className={`badge ${riskWordClass(r.stockoutRisk)}`}>{r.stockoutRisk}</span>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      <Card className="stat-card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <strong>{n.action}</strong>
          {n.insufficientData ? <span className="badge">insufficient data</span> : <span className="badge">{n.confidence} confidence</span>}
        </div>
        <p style={{ fontSize: 12.5, color: 'var(--text-muted)', margin: '6px 0 0' }}>{n.rationale}</p>
      </Card>
    </>
  );
}

/** Accessible bar sparkline for a dense monthly usage series (zeros are visible gaps). */
function UsageSparkline({ points }: { points: UsagePoint[] }) {
  if (points.length === 0) {
    return <p style={{ fontSize: 12.5, color: 'var(--text-muted)', margin: 0 }}>No usage history for this part.</p>;
  }
  const max = Math.max(1, ...points.map((p) => p.quantity));
  const total = points.reduce((a, p) => a + p.quantity, 0);
  const summary = `Monthly usage over ${points.length} months, total ${fmtInt(total)} units. Latest: ${points
    .slice(-3)
    .map((p) => `${p.month}: ${fmtInt(p.quantity)}`)
    .join(', ')}.`;
  return (
    <div role="img" aria-label={summary} style={{ display: 'flex', alignItems: 'flex-end', gap: 2, height: 56 }}>
      {points.map((p) => (
        <div
          key={p.month}
          title={`${p.month}: ${fmtInt(p.quantity)}`}
          style={{
            flex: 1,
            minWidth: 2,
            height: `${Math.max(2, (p.quantity / max) * 100)}%`,
            background: p.quantity > 0 ? 'var(--primary)' : 'var(--surface-2)',
            borderRadius: 2,
          }}
        />
      ))}
    </div>
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
