import { Icon } from './Icon';
import { aiLabelKinds, aiLabels, isKnownAiLabel } from './aiLabels';
import type { AiLabelKind } from './aiLabels';

export type { AiLabelKind };

interface AiLabelProps {
  /** One of the eight keys in `aiLabels`. An unrecognised value falls back to `recommendation`. */
  kind: string;
  className?: string;
}

/**
 * The shared AI-output chip (V3-DS-002) — one implementation of v3's eight output labels, replacing
 * the three hand-rolled `badge badge--demo` spans that grew across the app.
 *
 * **The text is always rendered.** Never colour alone — the label is the whole point of the chip,
 * and the tint only reinforces it.
 */
export function AiLabel({ kind, className }: AiLabelProps) {
  const known = isKnownAiLabel(kind);

  if (!known && import.meta.env.DEV) {
    // v3 falls back silently. A silent fallback here would mean a contract mismatch between the
    // server's vocabulary and this table renders as a confident "Recommendation" chip on something
    // that is not one — visible to nobody until a user asks why. The fallback stays (a missing chip
    // is worse than a wrong one), but it is no longer quiet.
    console.warn(
      `AiLabel: unknown label kind "${kind}"; falling back to "recommendation". ` +
        `Known kinds: ${aiLabelKinds.join(', ')}.`,
    );
  }

  const resolved: AiLabelKind = known ? kind : 'recommendation';
  const chip = aiLabels[resolved];

  return (
    <span
      className={`ai-label ai-label--${resolved}${className ? ` ${className}` : ''}`}
      title={chip.hint}
      data-kind={resolved}
    >
      <Icon name={chip.icon} />
      {chip.text}
    </span>
  );
}
