import { describe, expect, it } from 'vitest';
import { fmtInt, fmtNum, fmtPct, fmtSar, fmtSignPct, riskWordClass, rotationClass } from './format';

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
});
