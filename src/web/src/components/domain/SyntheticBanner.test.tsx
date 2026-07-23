import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { SyntheticBanner } from './SyntheticBanner';

describe('SyntheticBanner', () => {
  it('discloses that the data is synthetic and not Oracle Fusion', () => {
    render(<SyntheticBanner />);
    const note = screen.getByRole('note', { name: /synthetic demo data notice/i });
    expect(note).toBeInTheDocument();
    expect(note).toHaveTextContent(/synthetic demo data/i);
    expect(note).toHaveTextContent(/not Oracle Fusion/i);
    expect(note).toHaveTextContent(/synthetic-demo/i);
  });
});
