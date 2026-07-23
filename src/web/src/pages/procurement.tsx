import { useState } from 'react';
import { PageHeader } from '@/components/ui/PageHeader';
import { StatCard } from '@/components/ui/StatCard';
import { Card, CardHeader } from '@/components/ui/Card';
import { ScenarioSelect } from '@/components/ui/ScenarioSelect';
import { LoadingState, ErrorState, EmptyState } from '@/components/ui/states';
import { useProcurement, type ProcurementScenarioQuery } from '@/lib/api/orders';
import { fmtInt, fmtNum, riskWordClass } from '@/lib/format';

export default function Procurement() {
  const [service, setService] = useState('0.95');
  const [review, setReview] = useState('1');
  const [lead, setLead] = useState(''); // '' = use observed per-config lead time
  const [moq, setMoq] = useState('0');

  const query: ProcurementScenarioQuery = {
    serviceLevel: Number(service),
    reviewPeriodMonths: Number(review),
    minOrderQuantity: Number(moq),
    ...(lead ? { leadTimeMonths: Number(lead) } : {}),
  };
  const procurement = useProcurement(query);

  return (
    <>
      <PageHeader
        title="Procurement Optimisation"
        summary="How much to procure to balance demand, lead time and inventory cost — recommended as a range, not false precision."
        useCase="UC4"
        meta={[
          { label: 'Lead time', value: lead ? `${lead} months (override)` : 'observed per-config average' },
          { label: 'Assumptions', value: 'inbound = 0; open POs not yet integrated' },
        ]}
      />

      <div className="filter-bar">
        <ScenarioSelect label="Service level" value={service} onChange={setService} options={[{ value: '0.9', label: '90%' }, { value: '0.95', label: '95%' }, { value: '0.99', label: '99%' }]} />
        <ScenarioSelect label="Review period (months)" value={review} onChange={setReview} options={[{ value: '1', label: '1' }, { value: '2', label: '2' }, { value: '3', label: '3' }]} />
        <ScenarioSelect label="Lead time (months)" value={lead} onChange={setLead} options={[{ value: '', label: 'Observed' }, { value: '1', label: '1' }, { value: '2', label: '2' }, { value: '3', label: '3' }]} />
        <ScenarioSelect label="Min order qty" value={moq} onChange={setMoq} options={[{ value: '0', label: '0' }, { value: '10', label: '10' }, { value: '25', label: '25' }]} />
      </div>

      {procurement.isLoading ? (
        <LoadingState label="Computing safety stock and reorder points…" />
      ) : procurement.isError ? (
        <ErrorState title="API unavailable" message="Start the BeeEye API + PostgreSQL to load procurement recommendations." onRetry={() => void procurement.refetch()} />
      ) : !procurement.data || procurement.data.items.length === 0 ? (
        <EmptyState title="No procurement recommendations for this scenario" />
      ) : (
        <>
          <div className="grid grid--stats">
            <StatCard label="Configurations" value={fmtInt(procurement.data.meta.configurations)} icon="local_shipping" />
            <StatCard label="Total recommended" value={`${fmtInt(procurement.data.meta.totalRecommendedUnits)} units`} icon="inventory_2" hint="point estimate" />
            <StatCard label="Service level" value={`${Math.round(Number(service) * 100)}%`} icon="verified" />
          </div>

          <div style={{ height: 'var(--gap)' }} />

          <Card>
            <CardHeader title="Procurement recommendations" subtitle="Safety stock and order-up-to level per configuration. Hover a row for the rationale." />
            <div className="grid-scroll">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Configuration</th>
                    <th className="num">Demand/mo</th>
                    <th className="num">Lead (mo)</th>
                    <th className="num">Safety stock</th>
                    <th className="num">Reorder pt</th>
                    <th className="num">Recommended range</th>
                    <th>Stockout risk</th>
                    <th>Confidence</th>
                  </tr>
                </thead>
                <tbody>
                  {procurement.data.items.map((r) => (
                    <tr key={`${r.model}|${r.variant}`} title={r.rationale}>
                      <td>{r.model} · {r.variant}</td>
                      <td className="num">{fmtNum(r.demandMean, 1)}</td>
                      <td className="num">{fmtNum(r.leadTimeMonths, 1)}</td>
                      <td className="num">{fmtInt(r.safetyStock)}</td>
                      <td className="num">{fmtInt(r.reorderPoint)}</td>
                      <td className="num"><strong>{fmtInt(r.rangeLow)}–{fmtInt(r.rangeHigh)}</strong></td>
                      <td><span className={`badge ${riskWordClass(r.stockoutRisk)}`}>{r.stockoutRisk}</span></td>
                      <td>{r.confidence}</td>
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
