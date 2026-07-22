import { useState } from 'react';
import { PageHeader } from '@/components/ui/PageHeader';
import { StatCard } from '@/components/ui/StatCard';
import { Card, CardHeader } from '@/components/ui/Card';
import { ScenarioSelect } from '@/components/ui/ScenarioSelect';
import { LoadingState, ErrorState, EmptyState } from '@/components/ui/states';
import { useOrderOptimisation, type OrderScenarioQuery } from '@/lib/api/orders';
import { fmtInt, fmtPct, riskWordClass } from '@/lib/format';

export default function OrderOptimisation() {
  const [horizon, setHorizon] = useState('3');
  const [cover, setCover] = useState('1');
  const [moq, setMoq] = useState('0');
  const [multiple, setMultiple] = useState('1');

  const query: OrderScenarioQuery = {
    horizon: Number(horizon),
    targetCoverMonths: Number(cover),
    minOrderQuantity: Number(moq),
    orderMultiple: Number(multiple),
  };
  const orders = useOrderOptimisation(query);

  return (
    <>
      <PageHeader
        title="Order Optimisation"
        summary="Recommended monthly vehicle orders — demand forecast, netted against supply, within business constraints."
        useCase="UC1"
        meta={[
          { label: 'Separation', value: 'forecast → constraints → optimisation → recommendation' },
          { label: 'Assumptions', value: 'inbound/confirmed orders = 0 until PO data is integrated' },
        ]}
      />

      <div className="filter-bar">
        <ScenarioSelect label="Horizon (months)" value={horizon} onChange={setHorizon} options={[{ value: '3', label: '3' }, { value: '6', label: '6' }]} />
        <ScenarioSelect label="Target cover (months)" value={cover} onChange={setCover} options={[{ value: '1', label: '1' }, { value: '2', label: '2' }, { value: '3', label: '3' }]} />
        <ScenarioSelect label="Min order qty" value={moq} onChange={setMoq} options={[{ value: '0', label: '0' }, { value: '5', label: '5' }, { value: '10', label: '10' }]} />
        <ScenarioSelect label="Order multiple" value={multiple} onChange={setMultiple} options={[{ value: '1', label: '1' }, { value: '5', label: '5' }, { value: '10', label: '10' }]} />
      </div>

      {orders.isLoading ? (
        <LoadingState label="Forecasting demand and optimising orders…" />
      ) : orders.isError ? (
        <ErrorState title="API unavailable" message="Start the BeeEye API + PostgreSQL to load order recommendations." onRetry={() => void orders.refetch()} />
      ) : !orders.data || orders.data.items.length === 0 ? (
        <EmptyState title="No order recommendations for this scenario" />
      ) : (
        <>
          <div className="grid grid--stats">
            <StatCard label="Configurations" value={fmtInt(orders.data.meta.configurations)} icon="tune" />
            <StatCard label="Total recommended" value={`${fmtInt(orders.data.meta.totalRecommendedUnits)} units`} icon="shopping_cart" />
            <StatCard label="Scenario cover" value={`${cover} month${cover === '1' ? '' : 's'}`} icon="inventory" hint={`horizon ${horizon} mo`} />
          </div>

          <div style={{ height: 'var(--gap)' }} />

          <Card>
            <CardHeader title="Recommended orders by configuration" subtitle="Model · variant grain. Hover a row for the rationale." />
            <div className="grid-scroll">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Configuration</th>
                    <th className="num">Forecast</th>
                    <th className="num">Available</th>
                    <th className="num">Net need</th>
                    <th className="num">Order</th>
                    <th>Overstock</th>
                    <th>Understock</th>
                    <th>Confidence</th>
                    <th className="num">WMAPE</th>
                  </tr>
                </thead>
                <tbody>
                  {orders.data.items.map((r) => (
                    <tr key={`${r.model}|${r.variant}`} title={r.rationale}>
                      <td>{r.model} · {r.variant}</td>
                      <td className="num">{fmtInt(r.forecastDemand)}</td>
                      <td className="num">{fmtInt(r.available)}</td>
                      <td className="num">{fmtInt(r.netRequirement)}</td>
                      <td className="num"><strong>{fmtInt(r.recommendedQuantity)}</strong></td>
                      <td><span className={`badge ${riskWordClass(r.overstockRisk)}`}>{r.overstockRisk}</span></td>
                      <td><span className={`badge ${riskWordClass(r.understockRisk)}`}>{r.understockRisk}</span></td>
                      <td>{r.confidence}</td>
                      <td className="num">{fmtPct(r.wmape)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>
        </>
      )}
    </>
  );
}
