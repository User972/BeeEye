import { BarDistribution } from './BarDistribution';
import type { EvidenceSeries } from '@/lib/api/explainability';
import { fmtNum } from '@/lib/format';

const W = 560;
const H = 180;
const PAD = { top: 12, right: 12, bottom: 26, left: 44 };

interface EvidenceChartProps {
  series: EvidenceSeries;
}

/**
 * The explainability drawer's "Historical evidence" chart.
 *
 * Two shapes, because the engines produce two:
 *
 * - **One series** — UC5's additive risk factors, UC7's monthly usage. Delegated to
 *   {@link BarDistribution}, which the intelligence screens already use, rather than reimplemented.
 * - **Two series** — UC2's back-test, where the point is the *gap* between what the method predicted
 *   and what actually happened. A bar chart hides that comparison; two lines are the honest render.
 *
 * There is no third shape and no placeholder: where a screen has no chart to give the drawer, the
 * section is omitted entirely rather than filled with something drawn from nothing.
 */
export function EvidenceChart({ series }: EvidenceChartProps) {
  const points = series.points;
  if (points.length === 0) return null;

  const paired = points.some((p) => p.comparison !== null);

  if (!paired) {
    return (
      <BarDistribution
        rows={points.map((p, i) => ({ key: `${p.label}-${i}`, label: p.label, value: p.value }))}
        format={(v) => fmtNum(v, 1)}
        caption={`${series.valueLabel}, ${series.period}`}
      />
    );
  }

  const values = points.flatMap((p) => [p.value, p.comparison ?? 0]);
  const maxY = Math.max(1, ...values);
  const n = points.length;

  const xFor = (i: number) => PAD.left + (n === 1 ? 0 : (i / (n - 1)) * (W - PAD.left - PAD.right));
  const yFor = (v: number) => PAD.top + (1 - v / maxY) * (H - PAD.top - PAD.bottom);

  const path = (pick: (i: number) => number) =>
    points.map((_, i) => `${i === 0 ? 'M' : 'L'} ${xFor(i)} ${yFor(pick(i))}`).join(' ');

  const actualPath = path((i) => points[i]!.value);
  const comparisonPath = path((i) => points[i]!.comparison ?? 0);

  const tickIdx = [...new Set([0, Math.floor((n - 1) / 2), n - 1])];
  const comparisonLabel = series.comparisonLabel ?? 'Model';

  // The SVG is decorative to a screen reader; this sentence is the chart.
  const summary =
    `${series.valueLabel} against ${comparisonLabel}, ${series.period}. ` +
    points
      .map((p) => `${p.label}: ${fmtNum(p.value, 1)} vs ${fmtNum(p.comparison ?? 0, 1)}`)
      .join('; ') +
    '.';

  return (
    <figure style={{ margin: 0 }}>
      <svg viewBox={`0 0 ${W} ${H}`} width="100%" role="img" aria-label={summary} style={{ maxWidth: '100%' }}>
        {[0, 0.5, 1].map((t) => {
          const y = PAD.top + t * (H - PAD.top - PAD.bottom);
          return (
            <g key={t}>
              <line x1={PAD.left} y1={y} x2={W - PAD.right} y2={y} stroke="var(--chart-grid)" strokeWidth={1} />
              <text x={PAD.left - 6} y={y + 4} textAnchor="end" fontSize={9} fill="var(--text-faint)">
                {fmtNum(maxY * (1 - t), 0)}
              </text>
            </g>
          );
        })}

        <path d={comparisonPath} fill="none" stroke="var(--warn)" strokeWidth={1.5} strokeDasharray="4 3" />
        <path d={actualPath} fill="none" stroke="var(--primary)" strokeWidth={2} />

        {tickIdx.map((i) => (
          <text key={i} x={xFor(i)} y={H - 8} textAnchor="middle" fontSize={9} fill="var(--text-faint)">
            {points[i]?.label}
          </text>
        ))}
      </svg>

      {/* Legend in words, so the two series are distinguishable without relying on colour. */}
      <figcaption
        style={{ display: 'flex', gap: 14, flexWrap: 'wrap', fontSize: 11, color: 'var(--text-muted)', marginTop: 6 }}
      >
        <span>— {series.valueLabel}</span>
        <span>- - {comparisonLabel}</span>
      </figcaption>
    </figure>
  );
}
