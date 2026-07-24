/**
 * The eight AI-output labels from `docs/wireframes-v3/engine2.js` L28–37 (V3-DS-002).
 *
 * **All eight, including `dq`.** `README.md` documented seven and omitted "Data Quality";
 * `engine2.js` defines eight. The code is the source of truth, the README was corrected in the same
 * commit, and V3-CONFLICT-8 is closed.
 *
 * Keys are v3's, verbatim — including `low` and `dq`, which are not the words they render. The
 * server sends these keys (`ExplanationVocabulary.KeyFor`), so the two vocabularies agree without a
 * translation step in between.
 *
 * Kept in its own module rather than beside the component so `AiLabel.tsx` exports only components,
 * which is what keeps fast refresh working.
 */
export type AiLabelKind =
  | 'observed'
  | 'calculated'
  | 'forecast'
  | 'recommendation'
  | 'simulation'
  | 'demo'
  | 'low'
  | 'dq';

export interface LabelChip {
  text: string;
  icon: string;
  /** What this label means, as a `title` — the words alone are terse by design. */
  hint: string;
}

/**
 * Text and icon per label, verbatim from v3.
 *
 * **Colour lives in `components.css` as `.ai-label--{kind}`, not here.** v3 inlines its colours
 * because the prototype has no stylesheet; this application does, and a colour in a component is a
 * colour that cannot be themed, restyled or contrast-corrected without editing TypeScript.
 */
export const aiLabels: Record<AiLabelKind, LabelChip> = {
  observed: {
    text: 'Observed',
    icon: 'fact_check',
    hint: 'Read from a source system, unmodified.',
  },
  calculated: {
    text: 'Calculated',
    icon: 'calculate',
    hint: 'Derived arithmetically from observed facts. Deterministic and reproducible.',
  },
  forecast: {
    text: 'Forecast',
    icon: 'insights',
    hint: 'A projection beyond the observed window. It carries uncertainty.',
  },
  recommendation: {
    text: 'Recommendation',
    icon: 'auto_awesome',
    hint: 'An advisory action the engine proposes. A human decides.',
  },
  simulation: {
    text: 'Simulation',
    icon: 'science',
    hint: 'A what-if result under the assumptions you supplied, not a prediction.',
  },
  demo: {
    text: 'Demo Data',
    icon: 'biotech',
    hint: 'Derived from synthetic demo data, not Oracle Fusion.',
  },
  low: {
    text: 'Low Confidence',
    icon: 'help',
    hint: 'Produced from evidence too thin to rely on.',
  },
  dq: {
    text: 'Data Quality',
    icon: 'rule',
    hint: 'A data-quality finding rather than a business figure.',
  },
};

/** Every key, in v3's order. Exported so tests can assert the table is complete. */
export const aiLabelKinds = Object.keys(aiLabels) as AiLabelKind[];

export function isKnownAiLabel(kind: string): kind is AiLabelKind {
  return kind in aiLabels;
}
