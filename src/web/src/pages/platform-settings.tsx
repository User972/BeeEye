import type { ReactNode } from 'react';
import { PageHeader } from '@/components/ui/PageHeader';
import { StatCard } from '@/components/ui/StatCard';
import { Card, CardHeader } from '@/components/ui/Card';
import { Icon } from '@/components/ui/Icon';
import { LoadingState, ErrorState } from '@/components/ui/states';
import { useSettings } from '@/lib/api/settings';
import type { SettingsBand } from '@/lib/api/settings';
import { apiErrorMessage } from '@/lib/api/client';
import { fmtNum } from '@/lib/format';

function BandTable({ bands }: { bands: SettingsBand[] }) {
  return (
    <div style={{ overflowX: 'auto' }}>
      <table className="data-table">
        <thead>
          <tr>
            <th>Band</th>
            <th>Range</th>
          </tr>
        </thead>
        <tbody>
          {bands.map((b) => (
            <tr key={b.label}>
              <td>{b.label}</td>
              <td className="num">{b.range}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function WeightRow({ label, value }: { label: string; value: number }) {
  return (
    <tr>
      <td>{label}</td>
      <td className="num">{fmtNum(value, 0)}</td>
    </tr>
  );
}

/**
 * Settings (V3-GOV-010) — read-only. The platform's actual risk configuration surfaced for
 * transparency: the risk-factor weights, the risk and aging bands, the analysis date, the trailing
 * horizon and the cover ceiling. Every value is read straight from the engine; nothing here is editable.
 */
export default function SettingsPage() {
  const { data, isLoading, isError, error, refetch } = useSettings();

  let body: ReactNode;
  if (isLoading) {
    body = <LoadingState label="Loading the configuration…" />;
  } else if (isError || !data) {
    body = <ErrorState title="Could not load settings" message={apiErrorMessage(error)} onRetry={() => void refetch()} />;
  } else {
    body = (
      <>
        <Card>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <span className="badge risk-med">
              <Icon name="lock" />
              Current configuration · read-only
            </span>
            <span style={{ color: 'var(--text-faint)' }}>{data.note}</span>
          </div>
        </Card>

        <div style={{ height: 'var(--gap)' }} />

        <div className="grid grid--stats">
          <StatCard label="Analysis date" value={data.analysisDate} icon="event" hint="As-of date for risk & aging" />
          <StatCard label="Trailing months" value={data.trailingMonths} icon="history" hint="Demand horizon" />
          <StatCard label="Cover ceiling" value={fmtNum(data.coverMax, 0)} icon="speed" hint="Max months of stock cover scored" />
        </div>

        <div style={{ height: 'var(--gap)' }} />

        <div className="grid grid--cards">
          <Card>
            <CardHeader title="Risk-factor weights" subtitle={data.weights.note} />
            <div style={{ overflowX: 'auto' }}>
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Factor</th>
                    <th className="num">Weight</th>
                  </tr>
                </thead>
                <tbody>
                  <WeightRow label="Stock cover" value={data.weights.cover} />
                  <WeightRow label="Inventory aging" value={data.weights.aging} />
                  <WeightRow label="Demand trend" value={data.weights.demand} />
                  <WeightRow label="Holding cost" value={data.weights.holding} />
                  <WeightRow label="Lead time" value={data.weights.lead} />
                  <tr>
                    <td>
                      <strong>Sum</strong>
                    </td>
                    <td className="num">
                      <strong>{fmtNum(data.weights.sum, 0)}</strong>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </Card>

          <Card>
            <CardHeader title="Risk bands" subtitle="Overstock-risk score → band" />
            <BandTable bands={data.riskBands} />
          </Card>

          <Card>
            <CardHeader title="Aging bands" subtitle="Days in stock → band" />
            <BandTable bands={data.agingBands} />
          </Card>
        </div>
      </>
    );
  }

  return (
    <>
      <PageHeader
        title="Settings"
        summary="The platform's live risk configuration, shown read-only — exactly the values the decision models use."
        wireframed
        meta={[
          { label: 'Scope', value: 'Current configuration · read-only' },
          { label: 'Implementation', value: 'Operational' },
        ]}
      />
      {body}
    </>
  );
}
