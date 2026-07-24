# V3 Gap Analysis

> Three-way comparison: **original designs** (`docs/wireframes/`) → **current implementation** →
> **v3 designs** (`docs/wireframes-v3/`).
> Evidence base: [`v3-baseline.md`](v3-baseline.md), [`v3-design-inventory.md`](v3-design-inventory.md),
> [`v3-design-traceability.md`](v3-design-traceability.md).

> **⚠ Point-in-time (pre-S4–S6).** This gap analysis was written before the identity, write-path and
> Decision Log slices landed. Its highest-risk gaps — no write path, no identity, ADR-0006 accepted but
> unimplemented, UC8 "new", "7 live / 12 scaffolds" — are now **closed**: all eight use cases are live,
> the governed write path and Decision Log ship, Entra ID auth is enforced, and the module split is
> **11 operational / 8 scaffolded**. For current status see
> [`v3-progress.md`](v3-progress.md) and [`v3-design-traceability.md`](v3-design-traceability.md).

---

## 1. Executive summary

### 1.1 Degree of alignment

**v3 is purely additive.** Checksums prove `engine.js`, `support.js`, `.env.example` and all three
data files are **byte-identical** between v1 and v3. The entire delta is:

- `Meridian BI.dc.html` 247 KB → 518 KB (2.1×)
- a **new** `engine2.js` (61 KB)
- three new documents (`USE_CASE_MAPPING`, `DEMO_DATA_CATALOGUE`, `ACCEPTANCE_REPORT`)

**This is the single most consequential finding in the analysis.** Because `engine.js` is unchanged,
the existing `BeeEye.Analytics` port for UC2 (forecasting) and UC5 (inventory risk) — 332 passing
tests — is **not affected by v3 at all**. The programme carries no analytics-parity risk on the two
use cases where parity is contractually required.

| Dimension | Assessment |
|-----------|-----------|
| Design tokens | **Already aligned** — `tokens.css` matches v3's `:root` block value-for-value |
| Information architecture | **Was misaligned** (3 sections/10 items vs 6 groups/18) — **now aligned** (S1) |
| Screen coverage | 10 of 19 v3 screens exist; **8 genuinely new** screens outstanding |
| Analytics | UC1–UC7 live and passing; **UC8 is new** |
| Backend capability | **Read-only** — 44 endpoints, all `GET`. v3's governance features need writes |
| Identity | **Absent entirely** — blocks ADR-0006's named-actor requirement |

### 1.2 Major new capabilities in v3

1. **Executive Decision Cockpit (UC8)** — a ranked, cross-module decision feed with a multiplicative
   priority model.
2. **Governance group** — Decision Log, Data Health, Model & Data Lineage (all new).
3. **Shared explainability drawer** — one "Why this recommendation?" panel, 11 sections, reachable
   from every intelligence module.
4. **AI output label vocabulary** — 8 labels distinguishing Observed / Calculated / Forecast /
   Recommendation / Simulation / Demo Data / Low Confidence / Data Quality.
5. **Accept-recommendation flow** — every module routes proposals into one governed log.

### 1.3 Highest-risk gaps

| Rank | Gap | Why it is the risk it is |
|------|-----|--------------------------|
| 1 | **No write path** (44/44 endpoints are `GET`) | Every v3 governance feature mutates state. Idempotency, concurrency, transactions and audit all arrive at once with no existing pattern to copy. |
| 2 | **No identity** | ADR-0006 requires a *named human* on every decision. Building the Decision Log first would produce unattributable records — the exact failure the ADR exists to prevent. |
| 3 | **ADR-0006 Accepted but unimplemented** | The largest documentation-vs-code divergence in the repository. None of `Recommendation`, `RecStatusEvent`, `ManagementDecision`, `ApprovalStep`, `ActionOutcome` exists. |
| 4 | **v3's Decision Log contradicts ADR-0006** | v3 uses a mutable status dropdown, hard delete and `localStorage` — literally the ADR's rejected Option B and the behaviour its `Supersedes` field names. |
| 5 | **Performance already marginal** | `after-sales/service-intensity/summary` is **669 ms warm for 479 bytes** and does not improve when warm. The cockpit aggregates six such modules. |

### 1.4 Architectural impact

**Minimal, and that is the right answer.** The modular monolith, `IModule` composition, read-service
layering, shared analytics engine and React/TanStack SPA all accommodate v3 without change. v3
requires **two genuinely new architectural capabilities** — identity/authorization and a write path —
both of which are already anticipated by accepted ADRs (0006, 0007) and neither of which displaces
existing architecture.

No ADR needs to be superseded. **One new ADR is required** (authentication mechanism, session
lifecycle and role model), because no existing ADR settles it.

### 1.5 Recommended delivery strategy

Vertical slices, foundational first, sequenced by **real dependency** rather than by convention — see
[`v3-implementation-plan.md`](v3-implementation-plan.md). Notably, the conventional "design tokens
first" opening is **dropped** (tokens are already aligned, so it would deliver nothing), and the
Decision Log is deliberately sequenced *fourth*, behind identity and the write path.

---

## 2. Module-by-module findings

### 2.1 Executive (`cockpit`, `exec`)

- **Current.** One page at `/` showing a module list and four literal `—` placeholder stat cards,
  hinted "Wired with the decision workflow". `ExecutiveInsights` is a scaffold with a single
  module-info `GET`.
- **V3 target.** A Decision Cockpit ranking up to six cross-module decisions by
  `impact × urgency × confidence × controllability`, with severity, owner role, due window, evidence,
  a narrative summary and a 7-field financial block.
- **Gaps.** Functional: the entire decision feed. API: no endpoint. Data: none — it reads existing
  entities. Visual: complete rewrite. Test: no page tests exist.
- **Recommendation.** S2. Compose the feed in the shared `BeeEye.Analytics` engine (**not** by
  referencing other modules' types, preserving isolation). Priority model and 34 tests are **done**.

### 2.2 Sales Intelligence (`order`, `forecast`, `config`)

- **Current.** All three live with real endpoints (`/recommendations/order-optimisation`,
  `/forecasting/*`, `/sales-actuals/config-demand/*`) and analytics ports.
- **V3 target.** Same analytics, restyled to v3 layout, each row opening the explainability drawer
  with an "Add to order proposal" / "Create action" footer.
- **Gaps.** Visual: layout alignment. Functional: drawer entry points and the accept flow.
  **API/Data: none** — the calculations already exist.
- **Recommendation.** S3 (drawer) then S8 (layout). Low risk.

### 2.3 Supply Intelligence (`procurement`, `inventory`)

- **Current.** Both live. Inventory is the most mature screen (largest route chunk, 59.8 kB).
- **V3 target.** Restyled, plus a persistent **supplier synthetic-data disclosure banner** on
  procurement.
- **Gaps.** Visual only, plus reusing the existing `SyntheticBanner` on procurement.
- **Recommendation.** S8. The banner component already exists and is tested — reuse, do not duplicate.

### 2.4 After-Sales Intelligence (`correlation`, `parts`)

- **Current.** Both live over **synthetic** UC6/UC7 data, already labelled via `SyntheticBanner`.
- **V3 target.** Same, restyled.
- **Gaps.** Visual, plus a real **performance** gap (§1.3 rank 5).
- **Critical finding.** v3's fixtures use mulberry32 + FNV-1a-32 (`SEED = 20260531`); the repo uses
  SplitMix64 + FNV-1a-64. **Numeric parity is unreachable and should not be pursued** — CLAUDE.md
  already specifies UC6/UC7 follow `docs/product/use-cases/`, not the prototype.
- **Recommendation.** S8. Fix recomputation; do **not** re-port the fixture.

### 2.5 Governance (`decisions`, `datahealth`, `lineage`, `settings`) — the heart of the gap

- **Current.** `DecisionsAndOutcomes`, `DataQuality`, `ModelsAndExperiments` and `Audit` are all
  scaffolds — one module-info `GET` each. `platform-settings` is a placeholder page.
- **V3 target.** Decision Log with 9 statuses, owner/due editing, filters and CSV export; Data Health
  with 7 sources and a quality score; Lineage with a 6-stage pipeline and 8 metrics; Settings exposing
  risk weights and bands.
- **Gaps.** Everything: functional, data, API, authorization, audit, test.
- **Authorization gap.** Decisions require an authenticated principal and segregation of duties. None
  exists.
- **Recommendation.** S5 (records + write path) → S6 (Decision Log) → S7 (health, lineage, settings),
  after S4 (identity). Follow **ADR-0006, not the prototype**.

### 2.6 Platform (`ai`, `reports`, `ingest`, `data`, `methodology`, `integration`)

- **Current.** Only `data-management` exists, as a placeholder page over the `DataQuality` scaffold.
- **V3 target.** Five further screens, of which `ai` and `ingest` are the substantial ones.
- **Gaps.** Entirely new. `ai` and `ingest` are **XL**.
- **Recommendation.** S10/S11, last. The deterministic insight engine must be the default **and** the
  fallback; live AI may only reword, never compute.

---

## 3. Cross-cutting changes

| Area | Current | V3 target | Gap | Slice |
|------|---------|-----------|-----|-------|
| **Navigation** | 3 sections / 10 items | 6 groups / 18 items + phases + badges | ✅ groups done (S1); badges outstanding | S1 ✅ / S4 |
| **Global layout** | Rail + header + content | Same | ✅ aligned | S1 ✅ |
| **Design system** | OKLCH tokens, 10 primitives | Same tokens + 8 label chips + drawer | Tokens ✅; labels and drawer outstanding | S3 |
| **Responsive** | Broken ≤900px (rail filled viewport) | **v3 defines no mobile pattern at all** | ✅ fixed and defined independently | S1 ✅ |
| **Accessibility** | Basic roles; no skip link, no focus trap | v3 has Escape only; **no focus trap either** | ✅ shell done; drawer focus trap outstanding | S1 ✅ / S3 |
| **Validation** | Client + minimal server (UC1 scenario) | Forms not shown in v3 | Server-authoritative validation needed with writes | S5 |
| **Notifications** | None | Toast | Outstanding | S6 |
| **Error handling** | `ProblemDetails` on UC1 only | Not modelled in v3 | Consistent contract needed | S5 |
| **Audit logging** | None | Implied by Decision Log | `AuditEvent` needed | S5 |
| **Search / filter** | Per-screen filter options | Global filter model + chips + drill-down | Outstanding | S8 |
| **Pagination** | Server-side on inventory items | Client-side slicing only | **App exceeds v3** | — |
| **AI interactions** | None | Grounded Q&A + labels + confidence | Outstanding | S10 |
| **Authorization** | **None** | None in v3 either, but implied by owners/roles | Foundational | S4 |
| **Observability** | Structured logging | Not modelled | Extend with writes | S5 |
| **Performance** | 2 slow endpoints | Memoised feed (`_decCache`) | Fix recomputation | S8 |
| **Localisation** | `InvariantGlobalization` | English only | No gap | — |
| **Time zones** | UTC + pinned analysis date | Pinned analysis date `2026-06-30` | ✅ aligned | — |
| **Formatting** | `CultureInfo.InvariantCulture`, IBM Plex Mono for numerics | Same | ✅ aligned | — |

---

## 4. Conflicts and ambiguities

Full detail in [`v3-design-traceability.md`](v3-design-traceability.md#conflicts-requiring-a-decision).
Summary of the safe defaults chosen:

| ID | Conflict | Safe default chosen | Risk of proceeding |
|----|----------|--------------------|--------------------|
| **V3-CONFLICT-1** | v3's Decision Log is mutable + deletable + `localStorage`; ADR-0006 rejects exactly this | Keep v3's **look and vocabulary**, back it with the ADR's append-only model; guard-validated transitions replace the free dropdown; a terminal state replaces delete | Low — the ADR already settled it, and its `Supersedes` field names the prototype behaviour explicitly |
| **V3-CONFLICT-2** | v3's 9 statuses ≠ ADR-0006's 9 states | ADR machine is canonical; map v3 labels onto it; treat `Assigned`/`Snoozed` as **attributes**, not lifecycle states | Low |
| **V3-CONFLICT-3** | v3 loads fonts from Google CDN | Self-host (IBM Plex + Material Symbols are OFL-1.1) | Low — licensing explicitly permits redistribution |
| **V3-CONFLICT-4** | Irreconcilable PRNGs for synthetic data | **Do not chase parity on demo fixtures**; keep the spec-driven implementation | Low — CLAUDE.md already mandates this |
| **V3-CONFLICT-5** | v3 records an `owner` but has no auth | Sequence identity (S4) before human decisions (S6); system-generated records (S5) need no identity | **Medium** — the main sequencing constraint in the programme |
| **V3-CONFLICT-6** | v3 bug: default status `"Proposed"` is absent from its own status list | Use ADR-0006's `Generated`; do not reproduce the bug | Low |
| **V3-CONFLICT-7** | v3 is desktop-only | Define responsive behaviour independently | Low — already done in S1 |
| **V3-CONFLICT-8** | README says 7 AI labels; code defines 8 | Implement all 8 from `LABELS` (code is the source of truth) | Low |

**None of these blocks progress.** Each has a conservative default recorded and applied.

### Controls in v3 with no defined backend behaviour

| Control | v3 behaviour | Decision |
|---------|--------------|----------|
| Decision Log status dropdown | Free mutation of a local object | **Functional**, but re-specified as guarded transitions |
| Decision Log delete button | Removes the record | **Removed as unsafe** — replaced by a terminal state (ADR-0006 is append-only) |
| Owner / due-date inputs | Local edit | **Functional** once identity exists (S4/S6) |
| "Was this useful?" feedback | Sets local state | **Functional but advisory** — v3's own copy says it "does not automatically retrain the model" |
| Persona switcher | Changes emphasis only | **Informational only** — carries **no** authorization meaning; must never gate data |
| Live-AI mode | Optional reword | **Behind a feature flag** (S10), deterministic engine as default and fallback |

---

## 5. Removal and deprecation analysis

**No current functionality is deleted merely because v3 omits it.**

| Current capability | Present in v3? | Classification | Rationale |
|--------------------|----------------|----------------|-----------|
| URL routing / deep links / browser history | **No** — the prototype has no router | **Retain** | The app *exceeds* v3. The prototype's screen state is a plain string; removing routing would be a severe regression. |
| Server-side pagination (`/inventory/items`) | **No** — v3 slices client-side | **Retain** | v3 embeds its dataset; production must never load unbounded data into the browser. |
| Responsive shell (≤900px) | **No** — v3 is desktop-only | **Retain and improve** | Done in S1. |
| `SyntheticBanner` component | Yes (equivalent) | **Retain** | Already implemented and tested; reuse for procurement in S8. |
| Module registry (`/platform/modules`) | **No** | **Retain but relocate** | Useful operationally; belongs under Governance/Platform rather than on the cockpit landing page, which S2 repurposes. |
| Health / readiness endpoints | **No** | **Retain** | Operational necessity, outside v3's scope. |
| Use-case scaffold pages | Partially | **Retain until superseded** | Each is replaced by its own slice; removing them early would leave dead nav entries. |
| Management Actions (`actions`) | Present in code, **absent from v3's `startScreen` enum** | **Never built — do not build** | v3 supersedes it with the Decision Log. Nothing to remove. |
| `localStorage` decision persistence | Yes, in v3 | **Remove — unsafe** | ADR-0006 explicitly supersedes it. Never implemented in the app, so nothing is lost. |

**Requires a product decision:** none identified. Every difference resolves against an existing ADR,
an explicit CLAUDE.md rule, or a documented safe default.

---

## 6. Where documentation and implementation disagree

Recorded rather than glossed over, per the delivery rules:

| # | Disagreement | Evidence |
|---|--------------|----------|
| 1 | **ADR-0006 is "Accepted" but wholly unimplemented** | 9 persistence entities exist; none is `Recommendation`, `ManagementDecision`, `RecStatusEvent`, `ApprovalStep` or `ActionOutcome` |
| 2 | **ADR-0007 (idempotency and replay) is unimplemented** | No idempotency mechanism; no write endpoints at all |
| 3 | Canonical data model describes Clusters 7 & 8 in full | Neither cluster has any corresponding table |
| 4 | CLAUDE.md describes 19 modules with 7 "live" | Accurate — 12 are single-`GET` scaffolds, as documented |
| 5 | v3 README documents 7 AI labels | `engine2.js` `LABELS` defines 8 |
| 6 | v3 README lists 10 screens; the extension section implies 18 | 19 screens are actually addressable (`actions` is unlisted but reachable) |

Items 1–3 are the substantive ones and are exactly what slices S5/S6 close.
