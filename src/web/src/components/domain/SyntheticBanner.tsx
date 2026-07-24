import { Icon } from '@/components/ui/Icon';

/**
 * Prominent, accessible disclosure that UC6/UC7 run on deterministic synthetic demo data derived from
 * the real sales history — never presented as real after-sales/parts data or Oracle Fusion. Conveyed
 * with an icon + text (not colour alone).
 */
export function SyntheticBanner() {
  return (
    <div
      role="note"
      aria-label="Synthetic demo data notice"
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
        <strong>Synthetic demo data.</strong> After-sales &amp; spare-parts figures are generated
        deterministically from the real vehicle-sales history to make the correlations meaningful and
        reproducible. They are <strong>not</strong> real service/parts data and not Oracle Fusion. Provenance
        is tagged <code>synthetic-demo</code> on every response.
      </span>
    </div>
  );
}
