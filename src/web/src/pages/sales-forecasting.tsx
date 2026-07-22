import { useState } from 'react';
import { PageHeader } from '@/components/ui/PageHeader';
import { StatCard } from '@/components/ui/StatCard';
import { Card, CardHeader } from '@/components/ui/Card';
import { FilterSelect } from '@/components/ui/FilterSelect';
import { LoadingState, ErrorState, EmptyState } from '@/components/ui/states';
import { ForecastChart } from '@/components/charts/ForecastChart';
import {
  useForecast,
  useAccuracyBy,
  useForecastFilterOptions,
  type ForecastQuery,
} from '@/lib/api/forecasting';
import { fmtInt, fmtNum, fmtPct, fmtSignPct } from '@/lib/format';

const tendencyClass: Record<string, string> = {
  'over-forecasting': 'risk-med',
  'under-forecasting': 'risk-high',
  balanced: 'risk-low',
  insufficient: 'badge',
};

export default function SalesForecasting() {
  const [model, setModel] = useState('');
  const [location, setLocation] = useState('');
  const [holdout, setHoldout] = useState('6');
  const [ci, setCi] = useState('80');

  const options = useForecastFilterOptions();

  const base = {
    ...(model ? { model: [model] } : {}),
    ...(location ? { location: [location] } : {}),
  };
  // The "All" option emits '' → Number('') is 0; fall back to the server defaults instead of
  // sending holdout=0/ci=0 (which the backend would clamp to a degenerate 1-month back-test).
  const effectiveHoldout = Number(holdout) || 6;
  const effectiveCi = Number(ci) || 80;
  const forecastQuery: ForecastQuery = { ...base, holdout: effectiveHoldout, ci: effectiveCi, horizon: 6 };
  const forecast = useForecast(forecastQuery);
  // "Accuracy by model" is a cross-model comparison, so it must not inherit the selected Model
  // filter (that would collapse the table to a single row); only the Location filter applies.
  const accuracy = useAccuracyBy('model', {
    ...(location ? { location: [location] } : {}),
    holdout: effectiveHoldout,
  });

  const f = forecast.data?.forecast;

  return (
    <>
      <PageHeader
        title="Sales Forecasting"
        summary="Model-vs-baseline accuracy, forecast bias and future demand with confidence intervals."
        useCase="UC2"
        wireframed
        meta={[
          { label: 'Method', value: 'Back-test on holdout · lowest-WMAPE model selected' },
          { label: 'Months', value: options.data ? `${options.data.firstMonth} – ${options.data.lastMonth}` : '—' },
          { label: 'Data source', value: 'Sample dataset (not live Oracle Fusion)' },
        ]}
      />

      <div className="filter-bar">
        <FilterSelect label="Model" options={options.data?.models ?? []} value={model} onChange={setModel} />
        <FilterSelect label="Location" options={options.data?.locations ?? []} value={location} onChange={setLocation} />
        <FilterSelect label="Holdout (months)" options={['3', '6', '12']} value={holdout} onChange={setHoldout} allLabel="6" />
        <FilterSelect label="Confidence" options={['80', '90', '95']} value={ci} onChange={setCi} allLabel="80" />
      </div>

      {forecast.isLoading ? (
        <LoadingState label="Back-testing models…" />
      ) : forecast.isError ? (
        <ErrorState
          title="API unavailable or insufficient history"
          message="Start the BeeEye API + PostgreSQL, or widen the filters (≥3 months of history required)."
          onRetry={() => void forecast.refetch()}
        />
      ) : !f ? (
        <EmptyState title="No forecast available for these filters" />
      ) : (
        <>
          <div className="grid grid--stats">
            <StatCard label="Chosen model" value={f.chosenName} icon="model_training" hint={`over ${f.methods.length} candidates`} />
            <StatCard label="Back-test WMAPE" value={fmtPct(f.accuracy.wmape)} icon="target" hint={`holdout ${f.holdout} mo`} />
            <StatCard
              label="Forecast bias"
              value={fmtSignPct(f.accuracy.bias)}
              icon="balance"
              {...(f.accuracy.bias != null ? { delta: { label: f.accuracy.bias > 0 ? 'over-forecasting' : 'under-forecasting', direction: f.accuracy.bias > 0 ? 'neg' : 'pos' } } : {})}
            />
            <StatCard label="Next 6 months" value={`${fmtInt(f.futureSum)} units`} icon="trending_up" />
          </div>

          <div style={{ height: 'var(--gap)' }} />

          <Card>
            <CardHeader title="Demand forecast" subtitle={`${f.totalN} months history · ${f.horizon}-month projection · ${effectiveCi}% confidence`} />
            <ForecastChart history={f.history} backtest={f.backtest} future={f.future} />
          </Card>

          <div style={{ height: 'var(--gap)' }} />

          <div className="grid grid--cards">
            <Card>
              <CardHeader title="Model comparison" subtitle="Back-test accuracy by method (best chosen automatically)" />
              <div className="grid-scroll">
                <table className="data-table">
                  <thead>
                    <tr><th>Method</th><th className="num">WMAPE</th><th className="num">Bias</th><th className="num">MAE</th><th className="num">RMSE</th></tr>
                  </thead>
                  <tbody>
                    {f.methods.map((m) => (
                      <tr key={m.key} style={m.isChosen ? { background: 'var(--primary-weak)' } : undefined}>
                        <td>{m.name}{m.isBest ? <span className="badge risk-low" style={{ marginLeft: 6 }}>best</span> : null}</td>
                        <td className="num">{fmtPct(m.wmape)}</td>
                        <td className="num">{fmtSignPct(m.bias)}</td>
                        <td className="num">{fmtNum(m.mae, 1)}</td>
                        <td className="num">{fmtNum(m.rmse, 1)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </Card>

            <Card>
              <CardHeader title="Why this forecast" subtitle="Grounded in the computed metrics — association, not causation" />
              <ul className="capability-list">
                {f.explanation.points.map((p) => (
                  <li key={p}><span className="material-symbols-outlined" aria-hidden>chevron_right</span><span>{p}</span></li>
                ))}
              </ul>
            </Card>
          </div>

          <div style={{ height: 'var(--gap)' }} />

          <Card>
            <CardHeader title="Accuracy by model" subtitle="Where forecasts consistently over- or under-perform" />
            {accuracy.isLoading ? (
              <LoadingState />
            ) : accuracy.isError || !accuracy.data ? (
              <ErrorState onRetry={() => void accuracy.refetch()} />
            ) : (
              <div className="grid-scroll">
                <table className="data-table">
                  <thead>
                    <tr><th>Model</th><th className="num">Units</th><th className="num">WMAPE</th><th className="num">Bias</th><th>Tendency</th><th>Chosen model</th></tr>
                  </thead>
                  <tbody>
                    {accuracy.data.rows.map((r) => (
                      <tr key={r.value}>
                        <td>{r.value}</td>
                        <td className="num">{fmtInt(r.units)}</td>
                        <td className="num">{fmtPct(r.wmape)}</td>
                        <td className="num">{fmtSignPct(r.bias)}</td>
                        <td><span className={`badge ${tendencyClass[r.tendency] ?? 'badge'}`}>{r.tendency}</span></td>
                        <td style={{ color: 'var(--text-muted)' }}>{r.chosenModel}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Card>
        </>
      )}
    </>
  );
}
