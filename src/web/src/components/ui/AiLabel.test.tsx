import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AiLabel } from './AiLabel';
import { aiLabelKinds, aiLabels } from './aiLabels';

afterEach(() => {
  vi.restoreAllMocks();
});

describe('AiLabel — the eight v3 output labels (V3-DS-002)', () => {
  it('implements all eight labels from engine2.js, including Data Quality', () => {
    // V3-CONFLICT-8: README documented seven and omitted "Data Quality"; engine2.js's LABELS table
    // defines eight. The code is the source of truth, and the README was corrected to match.
    expect(aiLabelKinds).toEqual([
      'observed',
      'calculated',
      'forecast',
      'recommendation',
      'simulation',
      'demo',
      'low',
      'dq',
    ]);
  });

  it.each([
    ['observed', 'Observed'],
    ['calculated', 'Calculated'],
    ['forecast', 'Forecast'],
    ['recommendation', 'Recommendation'],
    ['simulation', 'Simulation'],
    ['demo', 'Demo Data'],
    ['low', 'Low Confidence'],
    ['dq', 'Data Quality'],
  ])('renders %s with v3’s exact text "%s"', (kind, text) => {
    render(<AiLabel kind={kind} />);

    expect(screen.getByText(text)).toBeInTheDocument();
  });

  it.each(aiLabelKinds)('carries the %s modifier class so colour lives in CSS, not TypeScript', (kind) => {
    const { container } = render(<AiLabel kind={kind} />);
    const chip = container.querySelector('.ai-label');

    expect(chip).toHaveClass(`ai-label--${kind}`);
    // The colour is a stylesheet concern. An inline style here would be a colour nobody can theme.
    expect(chip?.getAttribute('style')).toBeNull();
  });

  it.each(aiLabelKinds)('renders the %s icon from the v3 LABELS table', (kind) => {
    render(<AiLabel kind={kind} />);

    expect(screen.getByText(aiLabels[kind].icon)).toBeInTheDocument();
  });

  it('always renders the label text, never colour alone', () => {
    // The whole point of the chip. If this ever regresses to an icon-and-tint, a colour-blind or
    // monochrome reader loses the distinction between "Observed" and "Forecast" entirely.
    for (const kind of aiLabelKinds) {
      const { unmount } = render(<AiLabel kind={kind} />);
      expect(screen.getByText(aiLabels[kind].text)).toBeVisible();
      unmount();
    }
  });

  it('falls back to recommendation for an unrecognised key', () => {
    vi.spyOn(console, 'warn').mockImplementation(() => {});
    const { container } = render(<AiLabel kind="nonsense" />);

    expect(screen.getByText('Recommendation')).toBeInTheDocument();
    expect(container.querySelector('.ai-label')).toHaveClass('ai-label--recommendation');
  });

  it('warns in development when it falls back, so a contract mismatch is not silent', () => {
    // v3 falls back silently. A silent fallback means the server's vocabulary drifting from this
    // table renders as a confident "Recommendation" chip on something that is not one — visible to
    // nobody until a user asks why.
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});

    render(<AiLabel kind="nonsense" />);

    expect(warn).toHaveBeenCalledTimes(1);
    expect(warn.mock.calls[0]?.[0]).toContain('nonsense');
  });

  it('does not warn for a known key', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});

    render(<AiLabel kind="dq" />);

    expect(warn).not.toHaveBeenCalled();
  });

  it('carries an explanatory title so the terse wording is not the whole story', () => {
    render(<AiLabel kind="demo" />);

    expect(screen.getByTitle(/synthetic demo data/i)).toBeInTheDocument();
  });

  it('accepts an extra class without losing its own', () => {
    const { container } = render(<AiLabel kind="forecast" className="extra" />);
    const chip = container.querySelector('.ai-label');

    expect(chip).toHaveClass('ai-label--forecast');
    expect(chip).toHaveClass('extra');
  });
});
