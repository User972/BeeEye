/** Display formatting shared by the UC2/UC5 screens, mirroring the wireframe engine. */

export function fmtSar(n: number | null | undefined, opts?: { perDay?: boolean }): string {
  if (n === null || n === undefined || Number.isNaN(n)) return '—';
  const a = Math.abs(n);
  let s: string;
  if (a >= 1e9) s = `${(n / 1e9).toFixed(2)}B`;
  else if (a >= 1e6) s = `${(n / 1e6).toFixed(2)}M`;
  else if (a >= 1e3) s = `${(n / 1e3).toFixed(1)}K`;
  else s = Math.round(n).toLocaleString('en-US');
  return `SAR ${s}${opts?.perDay ? '/day' : ''}`;
}

export function fmtInt(n: number | null | undefined): string {
  if (n === null || n === undefined || Number.isNaN(n)) return '—';
  return Math.round(n).toLocaleString('en-US');
}

export function fmtNum(n: number | null | undefined, d = 1): string {
  if (n === null || n === undefined || Number.isNaN(n)) return '—';
  return n.toLocaleString('en-US', { minimumFractionDigits: d, maximumFractionDigits: d });
}

export function fmtPct(n: number | null | undefined, d = 1): string {
  if (n === null || n === undefined || Number.isNaN(n)) return '—';
  return `${n.toFixed(d)}%`;
}

export function fmtSignPct(n: number | null | undefined, d = 1): string {
  if (n === null || n === undefined || Number.isNaN(n)) return '—';
  return `${n >= 0 ? '+' : ''}${n.toFixed(d)}%`;
}

const riskClassMap: Record<string, string> = {
  Low: 'risk-low',
  Medium: 'risk-med',
  High: 'risk-high',
  Critical: 'risk-crit',
};

export function riskClass(band: string): string {
  return riskClassMap[band] ?? 'risk-low';
}
