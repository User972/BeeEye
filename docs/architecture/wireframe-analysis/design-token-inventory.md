# Design Token Inventory

> The authoritative catalogue of every reusable design token extracted verbatim from the "Meridian BI" wireframe (`Meridian BI.dc.html`), to be the single source of truth for the BeeEye React + TypeScript design system.

## Purpose & scope

Every value below is lifted **verbatim** from the POC stylesheet and its runtime colour-mapping code — nothing is invented, rounded, or "improved". The POC brand ("Meridian BI") is cosmetic; the production product is **BeeEye**, but the token *values*, *ratios*, and *semantics* carry forward unchanged so the migrated React UI is pixel- and colour-faithful to the signed-off wireframe.

The wireframe defines its tokens as **CSS custom properties** in three layers:

| Layer | Selector | Role |
| --- | --- | --- |
| Base (light) | `:root` | Default theme + all non-themed tokens (spacing, radius, AI accents, chart grid). |
| Dark override | `[data-theme="dark"]` | Overrides **only** the tokens that must change in dark; everything else **inherits** the base value. |
| Accent override | `[data-accent="teal" \| "indigo"]` | Re-points `--primary*` only; default accent is the blue baked into `:root`. |
| Density override | `[data-density="compact"]` | Re-points `--gap` / `--card-pad` only. |

**OKLCH is the native colour space.** Values are `oklch(L C H)` — Lightness `0–1`, Chroma `0–~0.4`, Hue `0–360°`. Perceptual lightness makes light/dark pairs trivial to reason about (shift `L`, keep `H`). Alpha blends use `color-mix(in oklch, …)`. The React system **must** keep OKLCH (Tailwind v4 / vanilla-extract / CSS-vars all support it) — do not down-convert to hex, or the light/dark symmetry and the tinting maths break.

> **"Inherits" in the Dark column** means the token is *not* redeclared under `[data-theme="dark"]`, so it resolves to the base `:root` value in both themes. These are load-bearing facts for the React theme contract — do not silently duplicate them into a dark palette.

---

## 1. Surface & structure colours

Backgrounds, cards, and borders. Hue stays in the cool blue-grey family (240–262°); dark mode drops lightness and lifts chroma slightly.

| Token | Light | Dark | Intended usage |
| --- | --- | --- | --- |
| `--bg` | `oklch(0.984 0.004 240)` | `oklch(0.19 0.02 262)` | App canvas / viewport background behind all content. |
| `--surface` | `#ffffff` | `oklch(0.235 0.02 262)` | Primary card & panel surface. **Only literal hex in the system** (pure white in light). |
| `--surface-2` | `oklch(0.972 0.005 240)` | `oklch(0.27 0.02 262)` | Recessed / secondary surface: table zebra, inset rows, filter bar, chips. |
| `--surface-3` | `oklch(0.955 0.006 245)` | `oklch(0.31 0.022 262)` | Tertiary surface: hover fills, badges, skeleton highlight stop. |
| `--border` | `oklch(0.905 0.006 250)` | `oklch(0.34 0.02 262)` | Default 1px hairline dividers and card outlines. |
| `--border-strong` | `oklch(0.82 0.01 250)` | `oklch(0.44 0.022 262)` | Emphasised border on hover / focus; scrollbar thumb. |

## 2. Text colours

Three-step neutral ramp shared across both themes' hue (258°); light/dark invert the lightness.

| Token | Light | Dark | Intended usage |
| --- | --- | --- | --- |
| `--text` | `oklch(0.26 0.018 258)` | `oklch(0.94 0.008 258)` | Primary body text, headings, numeric values. |
| `--text-muted` | `oklch(0.5 0.014 258)` | `oklch(0.72 0.016 258)` | Secondary labels, captions, sub-headers, axis labels. |
| `--text-faint` | `oklch(0.62 0.011 258)` | `oklch(0.6 0.016 258)` | Tertiary / disabled hints, placeholder-level text. |

## 3. Navigation colours

The left rail is a fixed dark gradient in **both** themes (it is chrome, not content), so several nav tokens are **only** defined in `:root` and intentionally inherit into dark.

| Token | Light | Dark | Intended usage |
| --- | --- | --- | --- |
| `--nav-bg` | `oklch(0.235 0.028 260)` | `oklch(0.165 0.02 262)` | Nav rail gradient top stop. |
| `--nav-bg-2` | `oklch(0.195 0.028 262)` | `oklch(0.13 0.018 262)` | Nav rail gradient bottom stop; persona toggle track. |
| `--nav-fg` | `oklch(0.93 0.008 258)` | *inherits* | Active/primary nav item label. |
| `--nav-muted` | `oklch(0.66 0.02 260)` | *inherits* | Section captions, inactive nav labels, persona hint. |
| `--nav-active-bg` | `oklch(0.3 0.04 260)` | `oklch(0.28 0.04 260)` | Selected / hovered nav item background. |
| `--nav-border` | `oklch(0.32 0.03 262)` | `oklch(0.28 0.025 262)` | Nav header/footer dividers. |

> Nav gradient recipe: `linear-gradient(180deg, var(--nav-bg), var(--nav-bg-2))`.

## 4. Primary / brand colours & accent variants

`--primary*` is the only user-switchable colour family (`data-accent`). Default = blue (in `:root`); `teal` and `indigo` are alternates. Each accent must also define its dark-mode `--primary*` — the table shows the **default blue** dark values; `teal`/`indigo` reuse their single declaration across themes (they override only the light-derived vars, so verify contrast if dark + non-default accent is offered in production).

| Token | Light (default blue) | Dark (default blue) | Intended usage |
| --- | --- | --- | --- |
| `--primary` | `oklch(0.55 0.13 248)` | `oklch(0.66 0.13 246)` | Primary action, active state, links, "Actual" chart series, focus. |
| `--primary-2` | `oklch(0.64 0.11 214)` | `oklch(0.72 0.11 210)` | Secondary/brand-gradient partner, "Forecast" series, diagnostics accent. |
| `--primary-weak` | `oklch(0.95 0.032 245)` | `oklch(0.3 0.05 250)` | Tinted pill/chip background for primary (e.g. "POC ENVIRONMENT" badge). |
| `--primary-ink` | `oklch(0.4 0.12 250)` | `oklch(0.82 0.1 245)` | Text/icon colour that sits on `--primary-weak`. |

**Accent overrides** (light-plane only; re-point `--primary`, `--primary-2`, `--primary-weak`):

| Accent | `--primary` | `--primary-2` | `--primary-weak` |
| --- | --- | --- | --- |
| `default` (blue, in `:root`) | `oklch(0.55 0.13 248)` | `oklch(0.64 0.11 214)` | `oklch(0.95 0.032 245)` |
| `teal` | `oklch(0.6 0.11 200)` | `oklch(0.66 0.1 178)` | `oklch(0.95 0.03 195)` |
| `indigo` | `oklch(0.52 0.16 280)` | `oklch(0.6 0.13 262)` | `oklch(0.95 0.035 285)` |

> Brand gradient recipe (logo, avatars, send button): `linear-gradient(135deg, var(--primary), var(--primary-2))`.

## 5. Risk scale colours

The four-stop risk ramp is the platform's most important semantic palette — it colours risk scores, the inventory aging bands, forecast-accuracy bars, and severity states. Green → yellow → orange → red with **rising chroma** as severity climbs. Dark mode lifts lightness one notch for contrast on dark surfaces.

| Token | Light | Dark | Band (score) | Intended usage |
| --- | --- | --- | --- | --- |
| `--risk-low` | `oklch(0.62 0.13 152)` | `oklch(0.68 0.14 152)` | **Low** 0–34 | Healthy inventory, good accuracy, positive/retain states. |
| `--risk-med` | `oklch(0.76 0.14 84)` | `oklch(0.79 0.14 84)` | **Medium** 35–59 | Watch-list, moderate accuracy, caution. |
| `--risk-high` | `oklch(0.67 0.16 52)` | `oklch(0.72 0.16 52)` | **High** 60–79 | High-attention units, poor accuracy, elevated risk. |
| `--risk-crit` | `oklch(0.57 0.2 27)` | `oklch(0.65 0.2 27)` | **Critical** 80–100 | Critical aging, liquidation, nav badges, error state. |

**Band → colour mapping** (from the runtime `riskVar` / `riskColors` maps — reproduce exactly in React):

| Domain band | Colour token |
| --- | --- |
| Risk `Low` / Aging `Healthy` | `--risk-low` |
| Risk `Medium` / Aging `Watch` | `--risk-med` |
| Risk `High` / Aging `High attention` | `--risk-high` |
| Risk `Critical` / Aging `Critical aging` | `--risk-crit` |
| Aging `New` (≤30 days) | `--primary` (not part of the risk ramp — treated as neutral-new) |

> Risk-band thresholds: `[34, 59, 79]`. Aging-band thresholds (days): `[30, 60, 90, 120]` → New ≤30 · Healthy ≤60 · Watch ≤90 · High attention ≤120 · Critical aging >120. Manufacture-age bands: `0–180 / 181–270 / 271–365 / 365+` days. See [Methodology](../../wireframes/docs/METHODOLOGY.md) and [Derived Metrics](../../wireframes/docs/DERIVED_METRICS.md).

## 6. Semantic status colours

Direction-of-change and warning semantics, distinct from the risk ramp (though they share hues by design). Used for KPI deltas and inline signalling.

| Token | Light | Dark | Intended usage |
| --- | --- | --- | --- |
| `--pos` | `oklch(0.6 0.13 152)` | `oklch(0.68 0.14 152)` | Positive delta / growth (green 152°). |
| `--neg` | `oklch(0.57 0.2 27)` | `oklch(0.66 0.2 27)` | Negative delta / decline (red 27°). |
| `--warn` | `oklch(0.76 0.14 84)` | *inherits* | Warning / attention (yellow 84°); e.g. "Apply controlled discount" pill. |

## 7. AI accent colours

Purple→blue pair reserved **exclusively** for AI-generated / GenAI-narration surfaces (Ask AI button, insight cards, confidence chips, gradient hairline borders). Not theme-overridden — identical in light and dark.

| Token | Light | Dark | Intended usage |
| --- | --- | --- | --- |
| `--ai-1` | `oklch(0.55 0.15 285)` | *inherits* | AI gradient start (purple 285°); "action" insight accent. |
| `--ai-2` | `oklch(0.62 0.12 224)` | *inherits* | AI gradient end (blue 224°). |

> AI gradient recipe (buttons, gradient-border cards, clipped-text icons): `linear-gradient(120deg, var(--ai-1), var(--ai-2))`. **Governance note:** this palette marks *narration only*. Per the target architecture, GenAI may narrate validated metrics but must never compute forecasts, risk, values, or decisions — the AI accent must never be reused to colour a computed number.

## 8. Chart & data-visualisation palette

BeeEye charts do **not** use a rotating categorical palette; series colour is **semantic** — driven by meaning (actual vs forecast) or by the value's band (risk/accuracy). This keeps the same colour meaning across every screen. See the [dataviz skill] conventions when extending.

| Chart element | Colour token / rule | Notes |
| --- | --- | --- |
| Grid lines | `--chart-grid` — light `oklch(0.925 0.005 250)` / dark `oklch(0.32 0.012 260)` | The one chart-specific token; the only element themed separately for charts. |
| "Actual" series | `--primary` (solid 3px line / bar) | Historical actuals. |
| "Forecast" series | `--primary-2` (3px **dashed** line) | Forward projection. |
| Confidence-interval band | `--primary` tint via `color-mix(in oklch, var(--primary) …%, transparent)` | Shaded CI around the forecast; back-test overlay uses the same family. |
| Forecast-accuracy bars | `wmape < 15 → --risk-low`, `< 30 → --risk-med`, else `--risk-high` | Lower WMAPE is better → greener. |
| Forecast-bias bars | `bias > 3 → --risk-high`, `< -3 → --primary`, else `--risk-low` | Over-forecast red, under-forecast blue, balanced green. |
| Risk / aging donut & bars | Risk-scale map (§5) via `riskVar` | Segments coloured by band. |
| Quadrant legend | `Low/Med/High/Crit → --risk-low/-med/-high/-crit` | Risk scatter legend dots. |

**Confidence pill** colour map (`confC` — AI answer & metric confidence):

| Confidence | Colour token | Pill background |
| --- | --- | --- |
| `High` | `--risk-low` | `color-mix(in oklch, <token> 13%, transparent)` |
| `Medium` | `--risk-med` | same 13% tint |
| `Low` | `--risk-high` | same 13% tint |

**Recommendation pill** colour map (Management Actions):

| Recommended action | Colour token |
| --- | --- |
| `Retain` | `--risk-low` |
| `Transfer stock` | `--primary-2` |
| `Start targeted promotion` | `--primary` |
| `Apply controlled discount` | `--warn` |
| `Pause / reduce procurement` | `--risk-high` |
| `Prioritise liquidation` | `--risk-crit` |
| `Investigate demand data` | `--text-muted` |

**Severity & insight accents:** severity `ok/medium/high → --risk-low/-med/-crit`; insight categories `risk → --risk-high`, `location → --primary`, `sales → --risk-low`, `forecast → --primary-2`, `action → --ai-1`, each paired with a `15%` `color-mix` tint background.

**Standard `color-mix` alpha stops in use:** `10%` (heat-cell fill), `13%` (confidence pill bg), `15%` (insight tint), `88%` (header glass: `color-mix(in oklch, var(--surface) 88%, transparent)`). Reproduce these exact stops as design-system opacity constants.

## 9. Elevation / shadow tokens

Three-step elevation ramp; dark mode swaps the cool tinted RGBA shadows for deeper neutral black.

| Token | Light | Dark | Intended usage |
| --- | --- | --- | --- |
| `--shadow-sm` | `0 1px 2px rgba(15,23,42,.06), 0 1px 3px rgba(15,23,42,.04)` | `0 1px 2px rgba(0,0,0,.3)` | Buttons, chips, subtle raised elements. |
| `--shadow-md` | `0 4px 16px rgba(15,23,42,.08), 0 1px 3px rgba(15,23,42,.05)` | `0 4px 16px rgba(0,0,0,.35)` | Cards, popovers, AI insight panels. |
| `--shadow-lg` | `0 12px 40px rgba(15,23,42,.16)` | `0 14px 44px rgba(0,0,0,.5)` | Modals, dialogs, filter/date overlays. |

> Ad-hoc `box-shadow: 0 2px 8px rgba(0,0,0,.25)` appears on the logo tile — treat as a one-off, not a token; migrate to `--shadow-sm`.

## 10. Spacing & density tokens

Two spacing tokens drive layout rhythm; density switches both. Comfortable is the default (`:root`), compact via `[data-density="compact"]`.

| Token | Comfortable (default) | Compact | Intended usage |
| --- | --- | --- | --- |
| `--gap` | `16px` | `11px` | Grid/flex gaps between cards, KPI tiles, chart blocks. |
| `--card-pad` | `18px` | `13px` | Internal padding of cards & panels. |

> Non-tokenised paddings (e.g. header `10px 22px`, nav item `9px 14px`, pill `2px 9px`) recur but are inline literals in the POC. Recommendation: promote the common set — `2/3/4/6/8/10/12/14/16/20/22px` — to a numeric spacing scale in React, mapping `--gap`/`--card-pad` onto it.

## 11. Radius tokens

One formal token plus a de-facto radius scale used inline. Standardise the scale in React.

| Token / value | Value | Intended usage |
| --- | --- | --- |
| `--radius` | `12px` | Cards, panels, primary containers. |
| `6px` (inline) | `6px` | Small toggles, segmented buttons, scrollbar thumb, skeleton small. |
| `7–8px` (inline) | `7px` / `8px` | Buttons, inputs, chips, icon buttons. |
| `9px` (inline) | `9px` | Nav items, logo tile, avatars. |
| `11px` (inline) | `11px` | Inner card inside a 2px gradient border (`--radius` − 1px). |
| `14px` (inline) | `14px` | AI conversation card. |
| `20px` (inline) | `20px` | Pills / chips (fully rounded look). |
| `50%` (inline) | `50%` | Status dots, remove-icon circles. |

> Proposed React radius scale: `sm 6` · `md 8` · `lg 9` · `xl 12 (=--radius)` · `2xl 14` · `pill 20` · `full 9999`.

## 12. Typography tokens

### Font families

| Role | Family (stack) | Weights loaded | Usage |
| --- | --- | --- | --- |
| UI / sans | `'IBM Plex Sans', system-ui, sans-serif` | 400 · 500 · 600 · 700 | All UI text, labels, headings, body. |
| Mono / numeric | `'IBM Plex Mono', monospace` | 400 · 500 · 600 | Tabular numbers, counts, code, chip figures — anywhere digit alignment matters. |
| Icons | `'Material Symbols Outlined'` (`opsz 20–24, wght 400, FILL 0, GRAD 0`) | 400 | Icon glyphs via `.msi` class with ligatures (`font-feature-settings: 'liga'`). |

### Font sizes (observed → proposed scale)

Body base is `14px`. The POC uses raw px inline; the table rationalises the observed values into a named scale for React. Sizes marked *core* recur heavily.

| Proposed name | px | Weight of usage | Typical usage |
| --- | --- | --- | --- |
| `2xs` | 10 / 10.5 | core | Uppercase eyebrow labels, nav captions, tiny badges. |
| `xs` | 11 / 11.5 | **most-used** | Captions, sub-labels, chip text, axis labels. |
| `sm` | 12 / 12.5 / 12.8 | core | Secondary body, buttons, table cells. |
| `base` | 13 / 13.5 | core | Body text, nav item labels (13.5). |
| `md` | 14 / 14.5 | core | **Body base (`14`)**, card titles. |
| `lg` | 15 / 16 | core | Screen titles (15), section headings (16). |
| `xl` | 17 / 18 / 19 | — | Sub-heading numerics, small icons (18–19). |
| `2xl` | 20 / 21 / 22 | — | KPI values, large icons. |
| `3xl` | 24 / 30 / 32 | — | Hero KPI numbers. |
| `4xl` | 40 / 44 | — | Empty-state & error glyphs. |

### Font weights

| Token | Value | Usage |
| --- | --- | --- |
| `regular` | 400 | Body copy, muted secondary text. |
| `medium` | 500 | Emphasised labels, de-emphasised brand suffix. |
| `semibold` | 600 | **Dominant emphasis** — titles, buttons, values, most bold text. |
| `bold` | 700 | Brand wordmark / logo only. |

> Icon glyph sizes track font-size and cluster at `13–21px` (`15/16/18/19` most common). Treat icon size as a size-scale alias, not a separate token.

## 13. Motion tokens

Reusable keyframes and their timing, defined in the POC `<style>`. Carry these into the React motion layer.

| Token (keyframe) | Timing | Definition / usage |
| --- | --- | --- |
| `spin` | `1s linear infinite` | `rotate(360deg)` — loading spinner (`progress_activity` icon). |
| `fadeUp` | `.4s ease` | `opacity 0→1` + `translateY(8px→0)` — screen/section enter. |
| `shimmer` | `1.4s linear infinite` | Background-position sweep for skeleton loaders (`.skl`). |
| `pulse` | (loop) | `opacity 1→.45→1` — attention pulse. |
| Skeleton fill (`.skl`) | — | `linear-gradient(90deg, --surface-2 25%, --surface-3 37%, --surface-2 63%)`, `border-radius 8px`. |
| Header hover | `filter: brightness(1.06)` | AI/gradient button hover; send button uses `1.08`. |

## 14. Naming convention for the React design system

Map each CSS custom property to a typed token path so light/dark/accent resolution stays in CSS while TypeScript gets autocomplete + Zod-checked usage.

| CSS custom property | Proposed token path | Category |
| --- | --- | --- |
| `--surface`, `--surface-2/3` | `color.surface.{base,2,3}` | surface |
| `--text`, `--text-muted/faint` | `color.text.{base,muted,faint}` | text |
| `--primary*` | `color.primary.{base,alt,weak,ink}` | brand |
| `--risk-low/med/high/crit` | `color.risk.{low,medium,high,critical}` | risk scale |
| `--pos/neg/warn` | `color.status.{positive,negative,warning}` | semantic |
| `--ai-1/2` | `color.ai.{1,2}` | AI accent |
| `--chart-grid` | `color.chart.grid` | chart |
| `--shadow-sm/md/lg` | `elevation.{sm,md,lg}` | shadow |
| `--gap`, `--card-pad` | `space.gap`, `space.cardPad` | spacing |
| `--radius` | `radius.xl` | radius |

**Contract rules for the React system:**
1. Keep OKLCH; keep the three-layer cascade (`:root` / `[data-theme]` / `[data-accent]` / `[data-density]`). Do not hard-code hex except the single `--surface` white.
2. Reproduce the band→colour and confidence/recommendation maps (§5, §8) as pure functions with unit tests — they are behavioural, not cosmetic.
3. Respect "inherits" rows: `--nav-fg`, `--nav-muted`, `--warn`, `--ai-1`, `--ai-2`, and all spacing/radius resolve to a single value across themes. One source, no duplication.
4. AI accent is a governance signal (narration-only), not a decorative colour — enforce in component review.

---

## Traceability

- **Source of truth (values):** [`../../wireframes/Meridian BI.dc.html`](../../wireframes/Meridian%20BI.dc.html) — `:root`, `[data-theme="dark"]`, `[data-accent]`, `[data-density]` blocks, and the `riskVar` / `confC` / recommendation colour maps in the embedded app code.
- **Bands & thresholds:** [`../../wireframes/docs/METHODOLOGY.md`](../../wireframes/docs/METHODOLOGY.md), [`../../wireframes/docs/DERIVED_METRICS.md`](../../wireframes/docs/DERIVED_METRICS.md).
- **Data semantics behind coloured dimensions:** [`../../wireframes/docs/DATA_DICTIONARY.md`](../../wireframes/docs/DATA_DICTIONARY.md).
- **Sibling wireframe-analysis docs:** component inventory, screen catalogue, and interaction-pattern inventory (this folder) consume this token catalogue as their shared vocabulary.
- **Downstream:** the BeeEye React + TypeScript design-system package derives its theme tokens, chart palette, and motion layer directly from this file.
