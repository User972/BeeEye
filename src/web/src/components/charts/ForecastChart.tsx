import type { BacktestPoint, FuturePoint, HistoryPoint } from '@/lib/api/forecasting';
import { fmtInt } from '@/lib/format';

interface ForecastChartProps {
  history: HistoryPoint[];
  backtest: BacktestPoint[];
  future: FuturePoint[];
}

const W = 820;
const H = 300;
const PAD = { top: 16, right: 16, bottom: 34, left: 48 };

/**
 * History + back-test + future forecast with a confidence band. Axes start at zero
 * (no misleading truncation); units are labelled; a text summary backs the SVG for
 * screen readers and print.
 */
export function ForecastChart({ history, backtest, future }: ForecastChartProps) {
  const n = history.length + future.length;
  if (n < 2) {
    return null;
  }

  const maxY = Math.max(
    1,
    ...history.map((h) => h.value),
    ...future.map((f) => f.hi),
    ...backtest.map((b) => b.forecast),
  );

  const xFor = (i: number) => PAD.left + (i / (n - 1)) * (W - PAD.left - PAD.right);
  const yFor = (v: number) => PAD.top + (1 - v / maxY) * (H - PAD.top - PAD.bottom);

  const histStart = history.length - backtest.length;
  const futureX = (j: number) => history.length + j;

  const historyPath = history.map((h, i) => `${i === 0 ? 'M' : 'L'} ${xFor(i)} ${yFor(h.value)}`).join(' ');
  const backtestPath = backtest.map((b, i) => `${i === 0 ? 'M' : 'L'} ${xFor(histStart + i)} ${yFor(b.forecast)}`).join(' ');

  const lastHist = history[history.length - 1];
  const futurePath =
    (lastHist ? `M ${xFor(history.length - 1)} ${yFor(lastHist.value)} ` : `M ${xFor(futureX(0))} ${yFor(future[0]?.value ?? 0)} `) +
    future.map((f, j) => `L ${xFor(futureX(j))} ${yFor(f.value)}`).join(' ');

  const bandTop = future.map((f, j) => `${j === 0 ? 'M' : 'L'} ${xFor(futureX(j))} ${yFor(f.hi)}`).join(' ');
  const bandBottom = [...future].reverse().map((f, j) => `L ${xFor(futureX(future.length - 1 - j))} ${yFor(f.lo)}`).join(' ');
  const bandPath = `${bandTop} ${bandBottom} Z`;

  const nowX = xFor(history.length - 1);
  const tickIdx = [...new Set([0, Math.floor(n / 3), Math.floor((2 * n) / 3), n - 1])];
  const labelAt = (i: number) => (i < history.length ? history[i]?.label : future[i - history.length]?.label) ?? '';

  const summary =
    `Forecast chart. History of ${history.length} months, ` +
    `future projection of ${future.length} months. ` +
    future.map((f) => `${f.label}: ${fmtInt(f.value)} units (range ${fmtInt(f.lo)}–${fmtInt(f.hi)})`).join('; ') + '.';

  return (
    <figure style={{ margin: 0 }}>
      <svg viewBox={`0 0 ${W} ${H}`} width="100%" role="img" aria-label={summary} style={{ maxWidth: '100%' }}>
        {/* y gridlines + labels */}
        {[0, 0.5, 1].map((t) => {
          const y = PAD.top + t * (H - PAD.top - PAD.bottom);
          const val = maxY * (1 - t);
          return (
            <g key={t}>
              <line x1={PAD.left} y1={y} x2={W - PAD.right} y2={y} stroke="var(--chart-grid)" strokeWidth={1} />
              <text x={PAD.left - 8} y={y + 4} textAnchor="end" fontSize={10} fill="var(--text-faint)">{fmtInt(val)}</text>
            </g>
          );
        })}

        {/* confidence band */}
        <path d={bandPath} fill="var(--primary)" opacity={0.14} />

        {/* now divider */}
        <line x1={nowX} y1={PAD.top} x2={nowX} y2={H - PAD.bottom} stroke="var(--border-strong)" strokeDasharray="3 3" />

        {/* series */}
        <path d={historyPath} fill="none" stroke="var(--primary)" strokeWidth={2} />
        {backtest.length > 0 ? (
          <path d={backtestPath} fill="none" stroke="var(--warn)" strokeWidth={1.5} strokeDasharray="4 3" />
        ) : null}
        <path d={futurePath} fill="none" stroke="var(--ai-1)" strokeWidth={2} strokeDasharray="6 3" />

        {/* x labels */}
        {tickIdx.map((i) => (
          <text key={i} x={xFor(i)} y={H - 12} textAnchor="middle" fontSize={10} fill="var(--text-faint)">{labelAt(i)}</text>
        ))}
      </svg>
      <figcaption style={{ display: 'flex', gap: 16, flexWrap: 'wrap', fontSize: 11.5, color: 'var(--text-muted)', marginTop: 6 }}>
        <Legend colour="var(--primary)" label="Actual (history)" />
        <Legend colour="var(--warn)" label="Back-test forecast" dashed />
        <Legend colour="var(--ai-1)" label="Future forecast" dashed />
        <Legend colour="var(--primary)" label="Confidence band" faded />
      </figcaption>
    </figure>
  );
}

function Legend({ colour, label, dashed, faded }: { colour: string; label: string; dashed?: boolean; faded?: boolean }) {
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
      <span
        style={{
          width: 16,
          height: dashed ? 0 : 10,
          borderTop: dashed ? `2px dashed ${colour}` : 'none',
          background: dashed ? 'transparent' : colour,
          opacity: faded ? 0.25 : 1,
          borderRadius: 2,
          display: 'inline-block',
        }}
      />
      {label}
    </span>
  );
}
