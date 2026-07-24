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

  it('accepts a custom label and message for other synthetic-data contexts (e.g. UC4 supplier data)', () => {
    render(
      <SyntheticBanner label="Demo supplier data notice">
        <strong>Demo supplier data.</strong> Supplier master and purchase-order history are not integrated.
      </SyntheticBanner>,
    );
    const note = screen.getByRole('note', { name: /demo supplier data notice/i });
    expect(note).toHaveTextContent(/supplier master and purchase-order history/i);
    // The default after-sales copy must not leak into an overridden banner.
    expect(note).not.toHaveTextContent(/After-sales & spare-parts/i);
  });
});
