import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { BarDistribution } from './BarDistribution';
import { ForecastChart } from './ForecastChart';

describe('BarDistribution', () => {
  it('exposes an accessible summary of the series (not colour alone)', () => {
    render(
      <BarDistribution
        rows={[
          { key: 'Low', label: 'Low', value: 147, band: 'Low' },
          { key: 'Critical', label: 'Critical', value: 2, band: 'Critical' },
        ]}
        format={(v) => String(v)}
        caption="Units by risk band"
      />,
    );
    const img = screen.getByRole('img');
    expect(img.getAttribute('aria-label')).toContain('Units by risk band');
    expect(img.getAttribute('aria-label')).toContain('Low: 147');
    expect(screen.getByText('147')).toBeInTheDocument();
  });
});

describe('ForecastChart', () => {
  const history = [
    { month: '2025-01', label: 'Jan 25', value: 10, isHold: false },
    { month: '2025-02', label: 'Feb 25', value: 12, isHold: true },
  ];
  const backtest = [{ month: '2025-02', label: 'Feb 25', actual: 12, forecast: 11 }];
  const future = [{ month: '2025-03', label: 'Mar 25', value: 13, lo: 9, hi: 17 }];

  it('renders an accessible chart with a text summary', () => {
    render(<ForecastChart history={history} backtest={backtest} future={future} />);
    const img = screen.getByRole('img');
    expect(img.getAttribute('aria-label')).toContain('Forecast chart');
    expect(img.getAttribute('aria-label')).toContain('Mar 25');
  });

  it('renders nothing when there is too little data', () => {
    const { container } = render(<ForecastChart history={[history[0]!]} backtest={[]} future={[]} />);
    expect(container.querySelector('svg')).toBeNull();
  });
});
