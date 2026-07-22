import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { RiskBadge } from './Badge';

describe('RiskBadge', () => {
  it('conveys severity with a text label, not colour alone', () => {
    render(<RiskBadge level="critical" />);
    expect(screen.getByText(/Critical risk/i)).toBeInTheDocument();
  });

  it('renders each risk level with its label', () => {
    const { rerender } = render(<RiskBadge level="low" />);
    expect(screen.getByText(/Low risk/i)).toBeInTheDocument();
    rerender(<RiskBadge level="high" />);
    expect(screen.getByText(/High risk/i)).toBeInTheDocument();
  });
});
