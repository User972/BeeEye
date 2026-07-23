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

const rotationClassMap: Record<string, string> = {
  Fast: 'risk-low',
  Medium: 'badge',
  Slow: 'risk-med',
  Dead: 'risk-crit',
};

/** Badge class for a UC3 rotation class (Fast/Medium/Slow/Dead). */
export function rotationClass(rotation: string): string {
  return rotationClassMap[rotation] ?? 'badge';
}

/** Badge class for a Low/Medium/High risk word. */
export function riskWordClass(word: string): string {
  return word === 'High' ? 'risk-high' : word === 'Medium' ? 'risk-med' : 'risk-low';
}

const demandClassMap: Record<string, string> = {
  Smooth: 'risk-low',
  Erratic: 'risk-med',
  Intermittent: 'risk-med',
  Lumpy: 'risk-high',
  Obsolescent: 'risk-high',
  InsufficientData: 'badge',
};

/** Badge class for a UC7 demand class (Smooth/Erratic/Intermittent/Lumpy/Obsolescent/InsufficientData). */
export function demandClassBadge(cls: string): string {
  return demandClassMap[cls] ?? 'badge';
}

const reliabilityMap: Record<string, string> = {
  High: 'risk-low',
  Medium: 'risk-med',
  Low: 'risk-high',
};

/** Badge class for a UC6 data-reliability tier (higher reliability reads as lower risk). */
export function reliabilityClass(tier: string): string {
  return reliabilityMap[tier] ?? 'badge';
}

/**
 * Badge class for a UC6 service-intensity index (fleet mean = 1.0). Higher intensity is a heavier
 * workshop burden, so it reads warmer; a null index (no fleet) is neutral.
 */
export function intensityClass(index: number | null | undefined, high: boolean): string {
  if (index === null || index === undefined) return 'badge';
  if (high) return 'risk-high';
  return index >= 1 ? 'risk-med' : 'risk-low';
}
