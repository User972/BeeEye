import type { ReactNode } from 'react';
import { Card } from './Card';
import { Icon } from './Icon';

interface StatDelta {
  label: string;
  direction: 'pos' | 'neg';
}

interface StatCardProps {
  label: string;
  value: ReactNode;
  icon?: string;
  delta?: StatDelta;
  hint?: string;
}

/** KPI stat tile. Trend direction is conveyed by an arrow + text, never colour alone. */
export function StatCard({ label, value, icon, delta, hint }: StatCardProps) {
  return (
    <Card className="stat-card">
      <span className="stat-card__label">
        {icon ? <Icon name={icon} /> : null}
        {label}
      </span>
      <span className="stat-card__value">{value}</span>
      {delta ? (
        <span className={`stat-card__delta stat-card__delta--${delta.direction}`}>
          <Icon name={delta.direction === 'pos' ? 'arrow_upward' : 'arrow_downward'} />
          {delta.label}
        </span>
      ) : null}
      {hint ? <span style={{ color: 'var(--text-faint)', fontSize: 11.5 }}>{hint}</span> : null}
    </Card>
  );
}
