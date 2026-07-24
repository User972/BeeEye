import type { ReactNode } from 'react';

export type RiskLevel = 'low' | 'medium' | 'high' | 'critical';

const riskClass: Record<RiskLevel, string> = {
  low: 'risk-low',
  medium: 'risk-med',
  high: 'risk-high',
  critical: 'risk-crit',
};

const riskLabel: Record<RiskLevel, string> = {
  low: 'Low',
  medium: 'Medium',
  high: 'High',
  critical: 'Critical',
};

/**
 * Risk indicator. Conveys severity with a text label and dot — not colour alone —
 * to meet the WCAG 2.2 AA requirement in the UX spec.
 */
export function RiskBadge({ level }: { level: RiskLevel }) {
  return (
    <span className={`badge ${riskClass[level]}`}>
      <span className="badge__dot" />
      {riskLabel[level]} risk
    </span>
  );
}

export function Badge({ children }: { children: ReactNode }) {
  return <span className="badge">{children}</span>;
}
