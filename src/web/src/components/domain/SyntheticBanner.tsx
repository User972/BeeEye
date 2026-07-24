import type { ReactNode } from 'react';
import { Icon } from '@/components/ui/Icon';

interface SyntheticBannerProps {
  /** Accessible name for the note region. */
  label?: string;
  /** Override the message. Defaults to the UC6/UC7 after-sales/parts disclosure. */
  children?: ReactNode;
}

/**
 * Prominent, accessible disclosure that a screen runs on synthetic / not-yet-integrated data — never
 * presented as real operational data or Oracle Fusion. Conveyed with an icon + text (not colour alone).
 *
 * The default message covers UC6/UC7 (after-sales & spare-parts synthetic demo data). UC4 passes its own
 * children for the supplier/PO wording (supplier master & PO history are not integrated), so the same
 * primitive carries every "this is not measured operational data" disclosure without duplicating markup.
 */
export function SyntheticBanner({ label = 'Synthetic demo data notice', children }: SyntheticBannerProps = {}) {
  return (
    <div
      role="note"
      aria-label={label}
      style={{
        display: 'flex',
        gap: 10,
        alignItems: 'flex-start',
        padding: '10px 14px',
        margin: '0 0 var(--gap)',
        borderRadius: 12,
        border: '1px solid var(--warn)',
        background: 'color-mix(in oklch, var(--warn) 12%, transparent)',
        fontSize: 12.5,
        lineHeight: 1.45,
      }}
    >
      <Icon name="science" />
      <span>
        {children ?? (
          <>
            <strong>Synthetic demo data.</strong> After-sales &amp; spare-parts figures are generated
            deterministically from the real vehicle-sales history to make the correlations meaningful and
            reproducible. They are <strong>not</strong> real service/parts data and not Oracle Fusion.
            Provenance is tagged <code>synthetic-demo</code> on every response.
          </>
        )}
      </span>
    </div>
  );
}
