import { describe, expect, it } from 'vitest';
import {
  demandClassBadge,
  fmtInt,
  fmtNum,
  fmtPct,
  fmtSar,
  fmtSignPct,
  intensityClass,
  reliabilityClass,
  riskWordClass,
  rotationClass,
} from './format';

describe('format', () => {
  it('abbreviates SAR amounts like the engine', () => {
    expect(fmtSar(46_747_500)).toBe('SAR 46.75M');
    expect(fmtSar(3235, { perDay: true })).toBe('SAR 3.2K/day');
    expect(fmtSar(950)).toBe('SAR 950');
    expect(fmtSar(null)).toBe('—');
  });

  it('formats integers and numbers', () => {
    expect(fmtInt(1234)).toBe('1,234');
    expect(fmtNum(12.345, 1)).toBe('12.3');
    expect(fmtInt(undefined)).toBe('—');
  });

  it('formats percentages with an explicit sign where relevant', () => {
    expect(fmtPct(12.34)).toBe('12.3%');
    expect(fmtSignPct(5)).toBe('+5.0%');
    expect(fmtSignPct(-9.3)).toBe('-9.3%');
    expect(fmtSignPct(null)).toBe('—');
  });

  it('maps rotation and risk words to badge classes', () => {
    expect(rotationClass('Fast')).toBe('risk-low');
    expect(rotationClass('Dead')).toBe('risk-crit');
    expect(rotationClass('unknown')).toBe('badge');
    expect(riskWordClass('High')).toBe('risk-high');
    expect(riskWordClass('Low')).toBe('risk-low');
  });

  it('maps UC7 demand classes to badge classes', () => {
    expect(demandClassBadge('Smooth')).toBe('risk-low');
    expect(demandClassBadge('Lumpy')).toBe('risk-high');
    expect(demandClassBadge('InsufficientData')).toBe('badge');
    expect(demandClassBadge('unknown')).toBe('badge');
  });

  it('maps UC6 reliability tiers so higher reliability reads as lower risk', () => {
    expect(reliabilityClass('High')).toBe('risk-low');
    expect(reliabilityClass('Medium')).toBe('risk-med');
    expect(reliabilityClass('Low')).toBe('risk-high');
  });

  it('bands the service-intensity index (fleet mean = 1.0)', () => {
    expect(intensityClass(1.3, true)).toBe('risk-high');
    expect(intensityClass(1.05, false)).toBe('risk-med');
    expect(intensityClass(0.7, false)).toBe('risk-low');
    expect(intensityClass(null, false)).toBe('badge');
  });
});
