interface BarRow {
  key: string;
  label?: string;
  value: number;
  band?: string; // maps to a risk class for colour
}

interface BarDistributionProps {
  rows: BarRow[];
  format: (value: number) => string;
  caption: string;
}

const bandClass: Record<string, string> = {
  Low: 'var(--risk-low)',
  Medium: 'var(--risk-med)',
  High: 'var(--risk-high)',
  Critical: 'var(--risk-crit)',
  'Critical aging': 'var(--risk-crit)',
  'High attention': 'var(--risk-high)',
  Watch: 'var(--risk-med)',
  Healthy: 'var(--risk-low)',
  New: 'var(--primary)',
};

/**
 * Accessible horizontal bar distribution. Bars are labelled with text values (not
 * colour alone); a visually-hidden caption summarises the series for screen readers.
 */
export function BarDistribution({ rows, format, caption }: BarDistributionProps) {
  const max = Math.max(1, ...rows.map((r) => r.value));
  return (
    <div role="img" aria-label={`${caption}. ${rows.map((r) => `${r.label ?? r.key}: ${format(r.value)}`).join('; ')}`}>
      <div style={{ display: 'grid', gap: 8 }}>
        {rows.map((r) => {
          const colour = r.band ? bandClass[r.band] ?? 'var(--primary)' : 'var(--primary)';
          return (
            <div key={r.key} style={{ display: 'grid', gridTemplateColumns: '120px 1fr auto', alignItems: 'center', gap: 10 }}>
              <span style={{ fontSize: 12.5, color: 'var(--text-muted)' }}>{r.label ?? r.key}</span>
              <div style={{ background: 'var(--surface-2)', borderRadius: 6, height: 18, overflow: 'hidden' }}>
                <div
                  style={{
                    width: `${(r.value / max) * 100}%`,
                    height: '100%',
                    background: colour,
                    borderRadius: 6,
                    minWidth: r.value > 0 ? 3 : 0,
                    transition: 'width 0.3s',
                  }}
                />
              </div>
              <span className="tabular" style={{ fontSize: 12.5, minWidth: 64, textAlign: 'right' }}>{format(r.value)}</span>
            </div>
          );
        })}
      </div>
    </div>
  );
}
