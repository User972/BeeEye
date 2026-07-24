# V3 Design Inventory

> **Status: Implemented (inventory complete).** A complete inventory of every screen, shared pattern,
> token and interactive state in the v3 prototype at `docs/wireframes-v3/`.
> Line references are to `docs/wireframes-v3/Meridian BI.dc.html` and `docs/wireframes-v3/engine2.js`
> at commit `cc8d073`.

## 0. How the prototype is built

The v3 prototype is a **single 518 KB HTML file** using a bespoke component runtime (`support.js`):

| Mechanism | Form |
|-----------|------|
| Root element | `<x-dc>` custom element |
| Templating | `{{ expr }}` interpolation |
| Iteration | `<sc-for list="{{ xs }}" as="x">` |
| Conditionals | `<sc-if value="{{ flag }}">` |
| Logic | A `class Component extends DCLogic` with `rv<Screen>()` view-model methods |
| Screen dispatch | `rvCommon()` + a `screen → rv*` map (line 3661) |
| Styling | One inline `<style>` block + heavy inline `style=""` attributes |

There is **no router** — screen state is `this.state.screen`, a plain string. There are no URLs, so
the prototype has **no deep-linking, no browser history and no bookmarkable state**. This is a gap the
production implementation must close (the SPA already uses TanStack Router).

Configurable props (declared in `data-props`, line 1806):

| Prop | Options | Default |
|------|---------|---------|
| `accent` | `blue` · `teal` · `indigo` | `blue` |
| `theme` | `light` · `dark` | `light` |
| `density` | `comfortable` · `compact` | `comfortable` |
| `persona` | `Executive` · `Business Analyst` · `IT / Data Steward` | `Executive` |
| `startScreen` | 18 screen ids | **`cockpit`** |

> **Finding V3-INC-1.** The `startScreen` enum lists 18 screens and **omits `actions`**, yet `actions`
> (Management Actions) is still a reachable screen with a full `rvActions()` view model. This confirms
> Management Actions is **superseded by the Decision Log** in v3 but was not removed. See the
> deprecation analysis in the gap report.

---

## 1. Navigation — 6 groups, 18 items

Source: `navGroupsDef()`, line 2384. Format: `[groupLabel, [[id, label, icon], …], phaseLabel]`.

| Group | Phase label | Screen id | Label | Material icon |
|-------|-------------|-----------|-------|---------------|
| **Executive** | Phase 5 | `cockpit` | Decision Cockpit | `dashboard` |
| | | `exec` | Executive Overview | `space_dashboard` |
| **Sales Intelligence** | Phase 1 | `order` | Order Optimization | `shopping_cart_checkout` |
| | | `forecast` | Forecast Accuracy | `trending_up` |
| | | `config` | Configuration Insights | `grid_view` |
| **Supply Intelligence** | Phase 2–3 | `procurement` | Procurement Optimization | `local_shipping` |
| | | `inventory` | Inventory Aging & Overstock | `inventory_2` |
| **After-Sales Intelligence** | Phase 4 | `correlation` | Sales ↔ Service Correlation | `handyman` |
| | | `parts` | Spare Parts Prediction | `settings_suggest` |
| **Governance** | *(none)* | `decisions` | Decision Log | `fact_check` |
| | | `datahealth` | Data Health | `health_and_safety` |
| | | `lineage` | Model & Data Lineage | `account_tree` |
| | | `settings` | Settings | `settings` |
| **Platform** | *(none)* | `ai` | Ask Decision Intelligence | `smart_toy` |
| | | `reports` | Reports & Exports | `description` |
| | | `ingest` | Data Ingestion | `cloud_upload` |
| | | `data` | Data Management | `database` |
| | | `methodology` | Methodology | `menu_book` |
| | | `integration` | Integration Blueprint | `hub` |

Plus one **unlisted** screen: `actions` (Management Actions) — reachable in code, absent from nav.
**Total addressable screens: 19.**

### 1.1 Navigation badges (`navBadges()`, line 2393)

Live counts rendered on nav items:

| Nav item | Badge value | Colour | Rule |
|----------|-------------|--------|------|
| `cockpit` | `decisionFeed().critical` | `--risk-crit` | Count of decisions with `severity === "high"` |
| `decisions` | count | `--primary` | Actions whose status is `Proposed`, `New` or `Under review` |
| `datahealth` | `"!"` | `--risk-med` | Shown when `dataHealth().mismatch` is non-empty |

Wrapped in `try/catch` returning `{}` — badges fail silent.

### 1.2 Active-item styling (`mkNav`, line 2461)

| State | `color` | `bg` | left `mark` | `weight` |
|-------|---------|------|-------------|----------|
| Active | `#fff` | `--nav-active-bg` | `--primary-2` | 600 |
| Inactive | `--nav-muted` | `transparent` | `transparent` | 500 |

---

## 2. Design tokens

The v3 `:root` block (HTML lines ~20–40) is **already reproduced in
`src/web/src/styles/tokens.css`**. Verified identical values for: `--bg`, `--surface`, `--surface-2/3`,
`--border`, `--border-strong`, `--text`, `--text-muted`, `--text-faint`, `--nav-bg`, `--nav-bg-2`,
`--nav-fg`, `--nav-muted`, `--nav-active-bg`, `--nav-border`, `--primary`, `--primary-2`,
`--primary-weak`, `--primary-ink`, `--risk-low/med/high/crit`, `--pos`, `--neg`, `--warn`,
`--chart-grid`, `--shadow-sm/md/lg`, `--ai-1`, `--ai-2`, `--gap` (16px), `--card-pad` (18px),
`--radius` (12px), and the full `[data-theme="dark"]` override set.

**Consequence: there is no design-token gap.** A "tokens first" slice would be a near no-op.

### 2.1 Tokens present in v3 but NOT in the app

| Token / mechanism | v3 usage | App |
|-------------------|----------|-----|
| `accent` variants (`teal`, `indigo`) | Prop-driven accent override | Absent |
| `density` (`compact`) | Prop-driven spacing override | Absent |
| `--radius-sm` / `--radius-lg` | — | App **adds** these (8px / 16px); v3 has only `--radius` |
| `--gap-sm` | — | App **adds** this (11px) |

The app is a **superset** on radii/gaps and a subset on accent/density.

### 2.2 Typography

`IBM Plex Sans` (400/500/600/700), `IBM Plex Mono` (400/500/600) and `Material Symbols Outlined`
(`opsz,wght,FILL,GRAD@20..24,400,0,0`), loaded from **Google Fonts CDN** with `preconnect`.

> **Finding V3-CSP-1.** CDN font loading conflicts with a strict Content-Security-Policy and creates a
> third-party runtime dependency. Numeric values are set in `IBM Plex Mono` throughout — this is
> semantic (tabular alignment), not decorative, so the mono face must be preserved. Recommendation:
> self-host both faces (both are OFL-1.1 licensed, so redistribution is permitted) rather than copying
> the CDN link. Recorded as a conflict in the gap analysis.

---

## 3. The shared explainability drawer — "Why this recommendation?"

**The single highest-value new pattern in v3.** Template at HTML lines 1710–1795; opened by
`openExplain(x)` (line 1940), closed by `closeExplain()` (line 1941).

### 3.1 Geometry and chrome

| Property | Value |
|----------|-------|
| Position | `fixed`, anchored right, full height |
| Width | **474px**, `max-width: 94vw` |
| Overlay | `rgba(15,23,42,.42)`, `z-index: 72` |
| Panel | `z-index: 73`, `background: var(--surface)`, `border-left: 1px solid var(--border)`, `box-shadow: var(--shadow-lg)` |
| Animation | `fadeUp .25s ease` |
| Header | `padding: 15px 18px`, bottom border; 34×34 rounded-9px gradient mark (`linear-gradient(120deg, var(--ai-1), var(--ai-2))`) with a 19px white `auto_awesome` glyph |
| Header title | 14px / 600 / line-height 1.35 |
| Header subtitle | `"Why this recommendation? · {module}"`, 11px, `--text-muted` |
| Body | `flex: 1`, `overflow-y: auto`, `padding: 15px 18px`, `gap: 16px` |
| Footer | Only when `exHasDecision`; `padding: 12px 18px`, top border, wrapped action buttons |

### 3.2 Body sections (in order)

| # | Section | Condition | Notes |
|---|---------|-----------|-------|
| 1 | Label chip | always | One of the 8 labels in §4 |
| 2 | **Recommendation** | `exHasRec` | `--primary-weak` bg, `--primary` border, radius 10px; uppercase 11px label |
| 3 | **Expected impact** | `exHasImpact` | 2-column grid of tiles; values in IBM Plex Mono 15px/600, tone-coloured |
| 4 | **Confidence** | **always** | 11px dot + label + optional `%`, then a reason list with `chevron_right` bullets |
| 5 | **Top drivers** | `exHasDrivers` | Numbered 20×20 rank chips; label 12.5px + mono detail |
| 6 | **Historical evidence** | `exHasChart` | Inline chart + optional note; optional period suffix |
| 7 | **Assumptions** | `exHasAssume` | Italic 11.5px, `info` icon in `--warn` |
| 8 | **Data lineage** | `exHasLineage` | Wrapped chips with icon + label |
| 9 | **Model / rule information** | `exHasModel` | 2-col grid: Model · Version · Recalculated · Horizon · Validation · Error |
| 10 | **Ownership** | `exHasDecision` | Two tiles: Owner · Status |
| 11 | **"Was this useful?"** | **always** | Feedback buttons + disclaimer: *"Feedback is recorded in the analytics platform only and does not automatically retrain the model."* |

### 3.3 Keyboard behaviour

`onEsc` (line 1827) implements a **priority chain**: `explain` → `selectedUnit` → `filterOpen` →
`dateOpen`. Only the topmost layer closes per Escape press, and `preventDefault()` is called.

> **Finding V3-A11Y-1.** The prototype implements Escape and overlay-click dismissal but **no focus
> trap and no focus restoration**. The app's existing `src/web/src/components/ui/Drawer.tsx` has the
> same gap. WCAG 2.2 AA requires both for modal dialogs. The production implementation must add them
> **to the shared `Drawer` primitive**, fixing every drawer at once rather than duplicating.

### 3.4 Drawer entry points

Every intelligence module opens the drawer with a module-specific payload, and each drawer footer
offers workflow actions that call `addAction(...)`:

| Screen | Line | Primary footer action |
|--------|------|----------------------|
| `order` | 1965 | "Add to order proposal" |
| `parts` | 2530 | "Prepare reorder" |
| `correlation` | 2611 | "Create action" |
| `procurement` | 2670 | "Prepare PO recommendation" |
| `config` | 2748 | "Create action" |
| `inventory` (unit) | 3061 | `duCreate` |
| `cockpit` | 3086–3088 | "Accept & log" · "Assign owner" · "Watchlist" |

---

## 4. AI output label vocabulary

Source: `LABELS`, `engine2.js` lines 28–37. **Eight** labels:

| Key | Text | Colour | Background | Icon |
|-----|------|--------|------------|------|
| `observed` | Observed | `--text-muted` | `--surface-3` | `fact_check` |
| `calculated` | Calculated | `--primary-ink` | `--primary-weak` | `calculate` |
| `forecast` | Forecast | `--primary-2` | `color-mix(in oklch, var(--primary-2) 15%, transparent)` | `insights` |
| `recommendation` | Recommendation | `--ai-1` | `color-mix(in oklch, var(--ai-1) 14%, transparent)` | `auto_awesome` |
| `simulation` | Simulation | `--primary` | `--primary-weak` | `science` |
| `demo` | Demo Data | `oklch(0.5 0.16 300)` | `color-mix(in oklch, oklch(0.55 0.16 300) 13%, transparent)` | `biotech` |
| `low` | Low Confidence | `--risk-high` | `color-mix(in oklch, var(--risk-high) 14%, transparent)` | `help` |
| `dq` | Data Quality | `--risk-med` | `color-mix(in oklch, var(--risk-med) 16%, transparent)` | `rule` |

> **Finding V3-INC-2 — resolved in S3.** `docs/wireframes-v3/README.md` documented **seven** labels
> and omitted `Data Quality`. The implementation is the source of truth: there are eight, all eight
> ship in `components/ui/AiLabel.tsx`, and that README was corrected in the same commit
> (V3-CONFLICT-8). Chip styling uses `color-mix(in oklab, …)` rather than v3's `oklch` interpolation
> space, matching the `.risk-*` and `.badge--demo` rules the app already shipped — the perceptual
> difference at these mix ratios is invisible, and one interpolation space across the stylesheet is
> worth more than parity with a prototype's arbitrary choice.

---

## 5. Governance screens (all new in v3)

### 5.1 Decision Log (`decisions`) — `rvDecisions()`, line 3100

**Status vocabulary (9):** `New` · `Under review` · `Accepted` · `Assigned` · `In progress` ·
`Completed` · `Snoozed` · `Rejected` · `Superseded`, coloured
`--primary-2` / `--warn` / `--risk-low` / `--primary` / `--primary` / `--risk-low` / `--text-muted` /
`--risk-crit` / `--text-faint`.

**Row fields:** `id`, `title`, `category`, `created`, `owner`, `due`, `impact`, `evidence`, `source`,
`hasRef`, `status`.

**Controls:** status `<select>` (free transition), owner input, due-date input, delete button, row
click → `openLoggedDecision(ref)` or category drill-down.

**Category → screen map:** Order Planning/Order proposal→`order`, Procurement→`procurement`,
Inventory→`inventory`, Configuration→`config`, Parts→`parts`, After-Sales→`correlation`.

**States:** `dlEmpty` (no actions), `dlNoMatch` (filter matches nothing), status filter chips
(hidden at count 0 unless active). **Export:** `admc_decision_log.csv` with 11 columns.

**Persistence:** `saveActions()` (line 1979) → `localStorage["meridian_actions"]`.

> **Finding V3-GOV-1 (P0 conflict).** The Decision Log is a **mutable record with a free status
> dropdown and hard delete**, persisted to `localStorage`. `docs/adr/0006-recommendation-decision-workflow.md`
> (status **Accepted**) explicitly **rejects** this design — its Option A is "ephemeral dashboard text"
> and Option B is "single mutable record", both rejected in favour of an append-only record plus a
> status-event log. The ADR's `Supersedes` field literally names *"POC behaviour — the 'Meridian BI'
> prototype stored management actions only in browser `localStorage`"*. The prototype simply never
> changed. **The ADR governs; the prototype's persistence and mutation model must not be copied.**

> **Finding V3-INC-3.** `addAction()` (line 1980) defaults `status: "Proposed"`, but `"Proposed"` is
> **not** in the Decision Log's 9-status list — newly created actions fall outside their own filter
> chips and render with the fallback `--text-muted` colour. `navBadges()` counts all three of
> `Proposed`/`New`/`Under review`, confirming the inconsistency is real rather than intentional.

### 5.2 Data Health (`datahealth`) — `rvDataHealth()` + `dataHealth()` (engine2.js line 560)

**Seven sources**, each with `name`, `system` (expected Fusion module), `status`, `rows`, `coverage`,
`note`, `kind`:

| Source | Expected system | Status | Kind |
|--------|-----------------|--------|------|
| Sales history | Fusion Order Management | Ready | workbook |
| Inventory on-hand | Fusion Inventory Management | Ready / Ready with assumptions | workbook |
| Supplier master & PO history | Fusion Procurement | **Demo data** | demo |
| Service / repair-order history | Fusion Service | **Demo data** | demo |
| Parts usage & parts inventory | Fusion Inventory / Service | **Demo data** | demo |
| Vehicle mileage & warranty claims | Fusion Service / CRM | **Blocked** | missing |
| Open purchase orders / inbound | Fusion Procurement | **Demo data** | demo |

**Status colours/icons:** Ready → `--risk-low`/`check_circle`; Ready with assumptions →
`--warn`/`info`; Demo data → `oklch(0.5 0.16 300)`/`biotech`; Partial → `--risk-med`/`warning`;
Blocked → `--risk-crit`/`block`.

**Score colour rule:** `> 85` → `--risk-low`; `> 70` → `--risk-med`; else `--risk-high`.
**Issue severities:** `ok`/`medium`/`high` → `check_circle`/`warning`/`error`.
**Export:** `admc_data_health.csv`.

### 5.3 Model & Data Lineage (`lineage`) — `lineage()` (engine2.js line 583)

**Six-stage pipeline:** Oracle Fusion ERP/CRM (`cloud`) → Secure read-only integration (`vpn_lock`) →
Curated analytics layer (`dataset`) → Forecast & decision models (`model_training`) → Explainability
service (`psychology`) → Decision Intelligence application (`insights`). Kind colours: `source`→
`--primary-ink`, `integration`→`--primary`, `curated`→`--primary-2`, `model`→`--ai-1`, `explain`→
`--ai-2`, `app`→`--risk-low`.

**Eight metrics**, each `confirmed` or `demo`: Recommended order mix · Procurement range (demo) ·
Inventory risk & aging · Sales forecast · Configuration demand · Service-intensity index (demo) ·
Spare-parts forecast (demo) · Executive priority score.

### 5.4 Settings (`settings`)

Default settings block (HTML line ~1808):

```
analysisDate: "2026-06-30", agingBands: [30,60,90,120], riskBands: [34,59,79],
trailingMonths: 3, coverTarget: 2, coverMax: 6, minHistory: 1,
weights: { cover: 30, aging: 25, demand: 20, holding: 15, lead: 10 },
holdout: 6, horizon: 6, ci: 80, forecastLevel: "model_variant", algo: "auto"
```

The risk weights (30/25/20/15/10) and bands (34/59/79) **match** ADR-0006 / the canonical data
model's `RiskScore` definition exactly.

---

## 6. Executive Decision Cockpit (`cockpit`, UC8) — the flagship new screen

`rvCockpit()` (line 3070) over `decisionFeed()` (engine2.js line 515).

### 6.1 The six decision rules

| ID | Title | Area | Severity | Kind | Owner role |
|----|-------|------|----------|------|------------|
| `D-ORD-1` | Increase order allocation | Order Planning | high | opportunity | Sales Planning Manager |
| `D-PRC-1` | Reduce procurement | Procurement | high | risk | Procurement Manager |
| `D-INV-1` | Redistribute aging inventory | Inventory | medium | risk | Inventory Manager |
| `D-SUP-1` | Review supplier delay exposure | Procurement | medium | risk | Procurement Manager (**demo**) |
| `D-PRT-1` | Increase parts stock | Parts | high | risk | Parts Manager (**demo**) |
| `D-SVC-1` | Prepare workshop capacity | After-Sales | medium | risk | After-Sales Manager (**demo**) |

Each rule emits at most **one** decision (it picks the top-ranked candidate), so the feed is 0–6 items.

### 6.2 Priority score (engine2.js line 511)

```
priorityScore = round(clamp(impact × urgency × confidence × controllability, 0, 1) × 100)
```

where `impactF = clamp(impact / 5_000_000, 0.15, 1)`. Purely multiplicative — a low factor
suppresses the whole score. Decisions sort by `priority` descending.

**Derived fields:** `dueDays` = 5 (high) / 12 (medium) / 20 (low); `confidence` = High > 0.75,
Medium > 0.5, else Low; `factors[]` = the four score inputs as 0–100 percentages.

**Aggregates:** `oppValue`, `riskValue`, `critical` (severity high), `lowConf` (confidence < 0.5),
`dueThisWeek` (dueDays ≤ 7), and a `financial` block (`revenueRisk` = riskValue × 0.4,
`workingCapital`, `holdingAvoid`, `procurementInvest`, `stockoutExposure`, `emergencyExposure`,
`serviceImpact`).

`decisionFeed` is memoised in `_decCache` keyed by `JSON.stringify(filters)`.

---

## 7. Synthetic-data generation

`engine2.js` line 7: `SEED = 20260531`. PRNG is **mulberry32** seeded by
`fnv1a_32("ADMC|" + key) ^ SEED` (lines 11–13), exposing `.f() .i(lo,hi) .pick(a) .norm(m,s) .range(lo,hi)`.

> **Finding V3-PAR-1.** The repository's `src/shared/BeeEye.Persistence/SyntheticData/DeterministicRandom.cs`
> uses **SplitMix64 + FNV-1a-64**. These are different algorithms producing entirely different
> sequences, so the app's UC6/UC7 synthetic data **cannot** numerically match the v3 prototype without
> reimplementing mulberry32/FNV-32. This is **correct and intentional**: `CLAUDE.md` states the
> UC6/UC7 engines "have no engine.js counterpart — their formulas are specified in
> `docs/product/use-cases/`". **Recommendation: do not chase numeric parity on synthetic demo
> fixtures.** Parity *is* required for UC2/UC5 (`engine.js`, byte-identical between v1 and v3) and
> *should* be pursued for UC8's `priorityScore`/`decisionFeed`, which operate on real data.

---

## 8. Global shell patterns

| Pattern | Detail |
|---------|--------|
| **Persona switcher** | 3 personas with short labels (Exec/Analyst/IT). `personaShowCharts = !personaIT` — the IT persona **hides charts**. Each has a hint string. Purely presentational; no authorization meaning. |
| **Global filters** | Dimensions: brand, model, variant, type, location, colour, interior (multi-select) + `ramadan` (yes/no) + `discountBand` (single). Rendered as removable chips. Filter button changes to `--primary-weak`/`--primary`/`--primary-ink` when any filter is active. Summary: *"N sales rows · M stock units match"*. |
| **Analysis date** | Explicit pinned date (default `2026-06-30`), never wall-clock. Matches the ADR-0006 guardrail. |
| **Theme toggle** | `light_mode`/`dark_mode` icon swap. |
| **Toast** | Bottom-centre, `--nav-bg` background, `check_circle` in `--risk-low`, `fadeUp .25s`, z-index 80. |
| **Loading state** | `rvLoading()` — *"Reading workbooks, validating schema and computing metrics…"*, empty nav. |
| **Error state** | `isError` + `errorMsg`. |
| **Drill-down** | `drilldown({ screen, filter })` navigates and applies filters atomically — the cross-module navigation primitive. |
| **CSV export** | `exportCSV(name, rows)` with RFC-4180 escaping (`csvEscape`), Blob + object-URL download, 400 ms cleanup, try/catch → toast on failure. |

---

## 9. Screen coverage summary

| # | Screen id | Title | Group | New in v3? |
|---|-----------|-------|-------|-----------|
| 1 | `cockpit` | Executive Decision Cockpit | Executive | **Yes (UC8)** |
| 2 | `exec` | Executive Overview | Executive | No |
| 3 | `order` | Monthly Order Optimization | Sales | **Yes (UC1)** |
| 4 | `forecast` | Sales Forecasting | Sales | No |
| 5 | `config` | Configuration Demand Intelligence | Sales | **Yes (UC3)** |
| 6 | `procurement` | Procurement Optimization | Supply | **Yes (UC4)** |
| 7 | `inventory` | Inventory Intelligence | Supply | No |
| 8 | `correlation` | Sales & After-Sales Correlation | After-Sales | **Yes (UC6)** |
| 9 | `parts` | Spare Parts Demand Prediction | After-Sales | **Yes (UC7)** |
| 10 | `decisions` | Decision Log | Governance | **Yes** |
| 11 | `datahealth` | Data Health | Governance | **Yes** |
| 12 | `lineage` | AI Model & Data Lineage | Governance | **Yes** |
| 13 | `settings` | POC Settings | Governance | No |
| 14 | `ai` | AI Business Analyst | Platform | No |
| 15 | `reports` | Reports & Exports | Platform | No |
| 16 | `ingest` | Data Ingestion | Platform | No |
| 17 | `data` | Data Management | Platform | No |
| 18 | `methodology` | Methodology & Assumptions | Platform | No |
| 19 | `integration` | Integration Blueprint | Platform | No |
| — | `actions` | Management Actions | *(unlisted)* | No — **superseded** |

**19 addressable screens; 8 genuinely new in v3.**

---

## 10. States NOT represented in the v3 prototype

The prototype is a client-side demo over embedded data, so it never shows several states the
production implementation is nonetheless obliged to handle:

- Authentication / session expiry / permission-denied (no auth exists in the prototype)
- Server error (5xx), network failure, request timeout, retry, cancellation
- Server-side validation failure
- Conflict / stale-record (optimistic concurrency)
- Rate limiting
- Pagination beyond client-side slicing
- Offline / connectivity loss
- Per-screen empty states (only the Decision Log has explicit `dlEmpty` / `dlNoMatch`)
- Any mobile-specific navigation pattern (see below)

> **Finding V3-RESP-1.** The prototype declares `<meta name="viewport">` and is built with a fixed
> left nav rail; it contains **no `@media` breakpoints for the nav rail** and no mobile navigation
> pattern. The v3 designs are effectively **desktop-only**. The production implementation must define
> responsive behaviour itself — the existing app already collapses the shell at `max-width: 900px`
> (`components.css`), which is a capability the app has and v3 lacks.
