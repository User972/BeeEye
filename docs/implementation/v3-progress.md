# V3 Implementation Progress

> Living record of each vertical slice. Statuses: `Not started` · `Analysing` · `Ready` ·
> `Implementing` · `In review` · `Blocked` · `Complete`.
> Last updated: 2026-07-23.

## Summary

| Slice | Title | Priority | Status |
|-------|-------|----------|--------|
| S0 | Dependency & warning hygiene | P1 | Ready |
| **S1** | **Application shell & grouped navigation** | P1 | **Complete** |
| **S2** | **UC8 Executive Decision Cockpit** | P1 | **Complete** |
| S3 | Explainability drawer & AI label system | P1 | Ready |
| S4 | Identity, roles & authorization | P0 | Analysing |
| S5 | Recommendation records & write path | P0 | Ready |
| S6 | Decision Log & human decisions | P1 | Blocked (needs S4, S5) |
| S7 | Data Health, Lineage & Settings | P2 | Not started |
| S8 | Intelligence-screen alignment & performance | P2 | Not started |
| S9 | Persona, accent, density | P3 | Not started |
| S10 | Ask Decision Intelligence | P2 | Not started |
| S11 | Ingestion, Reports, Methodology, Integration | P3 | Not started |
| S12 | E2E, visual, a11y, coverage hardening | P1 | Not started |

## Test totals

| Milestone | Backend | Web | Total |
|-----------|---------|-----|-------|
| Baseline | 387 | 17 | **404** |
| After S1 | 387 | 45 | **432** |
| After S2 (analytics complete) | 439 | 45 | 484 |
| **After S2 (complete)** | **470** | **67** | **537** |

Backend breakdown after S2: `BeeEye.UnitTests` **36** (was 18) · `BeeEye.Analytics.Tests` **384**
(was 332) · `BeeEye.ArchitectureTests` 4 · `BeeEye.IntegrationTests` **46** (was 33).
Web: 67 (was 17). All green, 0 failures, 0 skipped.

---

## S1 — Application shell & grouped navigation · **Complete**

- **Requirements.** V3-NAV-001, V3-NAV-002, V3-NAV-003.
- **Code.** ✅ `config/navigation.ts` (6 groups, phase labels, v3 ordering/icons/labels);
  `NavRail.tsx` (group headings, `aria-current`, mobile props); `RootLayout.tsx` (skip link, scrim,
  Escape, route-change dismissal); `AppHeader.tsx` (toggle with `aria-expanded`/`aria-controls`);
  `components.css` (active mark, focus-visible, skip link, mobile rail, reduced motion);
  `vitest.setup.ts` (jsdom `scrollTo` stub).
- **Tests.** ✅ 17 registry tests + 15 shell/rail component tests. Web suite 17 → **45**.
- **Documentation.** ✅ Baseline, design inventory, traceability, gap analysis, plan, risks, strategy.
- **Visual comparison.** ⚠️ Structural assertions only — no visual-regression tooling exists yet (S12).
- **Verification.** typecheck ✅ · lint ✅ · web build ✅ · 45/45 web ✅ · **387/387 backend regression ✅**.
- **Bundle impact.** CSS 11.29 → 12.61 kB raw (+0.33 kB gzip); JS 323.23 → 324.66 kB raw (+0.49 kB gzip).
- **Known gaps.** Nav badges (V3-NAV-004) need the cockpit feed and decision counts — deferred to S2/S4.
  No entries added for v3's 9 unbuilt screens, deliberately, to avoid dead links.
- **Decisions recorded.** British spelling ("Optimisation") retained over v3's US spelling, per repo
  convention. Fixed a **pre-existing** responsive defect where the rail kept `height: 100vh` at
  ≤900px and pushed all content below the fold.
- **Risks.** R-19 (mobile) reduced to Mitigating; R-11 (accessibility) reduced to Mitigating.
- **Next action.** None — slice closed.

## S2 — UC8 Executive Decision Cockpit · **Complete**

- **Requirements.** V3-UC08-001…007 and V3-UC08-009 implemented; **V3-UC08-008 blocked** (see below).

### Architecture — the `IDecisionSignalProvider` seam

The cockpit spans seven use cases, but the boundary rules forbid a module referencing another module.
Rather than duplicate five read services inside ExecutiveInsights, a **published contract** was added:
`BeeEye.Analytics/Decisions/IDecisionSignalProvider.cs`. Each context implements it and registers
itself; ExecutiveInsights injects `IEnumerable<IDecisionSignalProvider>` and only ranks, summarises
and narrates.

- Every rule stays with the context that owns its data.
- ExecutiveInsights references **no module** — only `BeeEye.Analytics`. The 4 architecture tests still
  pass, proving isolation held.
- Providers run **sequentially by design**: they share the request-scoped `DbContext`, which is not
  thread-safe, so concurrent execution would race on the connection.

### Code

- ✅ `DecisionPriority.cs` — multiplicative priority model, impact normalisation, severity→due-days,
  confidence bands/weights, ranked factors. Ported from `engine2.js` L511–559 with
  `MidpointRounding.AwayFromZero` to match JavaScript's `Math.round`.
- ✅ `DecisionFeed.cs` — `Decision` record with derived members, `Rank` (totally ordered) and
  `Summarise`.
- ✅ `IDecisionSignalProvider.cs` — the cross-context contract.
- ✅ Five providers: `OrderDecisionSignalProvider` (D-ORD-1), `ConfigurationDecisionSignalProvider`
  (D-PRC-1), `InventoryDecisionSignalProvider` (D-INV-1), `SparePartsDecisionSignalProvider`
  (D-PRT-1), `AfterSalesDecisionSignalProvider` (D-SVC-1).
- ✅ `DecisionFeedService` — resilient aggregation; a failing context becomes a reported **gap**, not a
  500 and not a silent omission. Exception detail is logged, never returned to the browser.
- ✅ `GET /api/v1/executive-insights/decision-feed`; module status `scaffolded` → `operational`.
- ✅ `lib/api/executive.ts` typed client + `useDecisionFeed` hook.
- ✅ `pages/executive-cockpit.tsx` rewritten; four `—` placeholders replaced with live figures.

### Monetary impact is derived from real data

| Rule | Impact basis |
|------|--------------|
| D-ORD-1 | Net requirement × units-weighted average selling price from `SalesFacts` |
| D-PRC-1 | Stock still held × that configuration's own average selling price |
| D-INV-1 | Annual holding cost from each unit's own `HoldingCostPerDay` — no rate assumed |
| D-PRT-1 | Recommended quantity × catalogue `Part.UnitCost` |
| D-SVC-1 | Labour hours × an **assumed** rate — see below |

**Stated assumption (D-SVC-1).** Service history records labour hours but carries no monetary rate.
Workshop exposure is valued at an assumed SAR 350/hour, exposed as a constructor parameter so it can
be configured, and **written into the decision's evidence text** so a reader always sees the basis.
The decision is also flagged `isDemo`.

### Live output (seeded dataset, verified)

```
5 decisions · 2 critical · 2 due this week · 0 gaps
D-ORD-1 p=31 Medium Opportunity  SAR 72,095,231  Increase order allocation — Haval H9 MX
D-SVC-1 p=16 Medium Risk  demo   SAR 15,645,308  Prepare workshop capacity — Haval H9 cohort
D-PRT-1 p=13 High   Risk  demo   SAR  1,254,300  Increase stock for Engine Control Module
D-PRC-1 p= 7 High   Risk         SAR    652,831  Reduce procurement — ES 350 MX · Pearl White
D-INV-1 p= 5 Medium Risk         SAR     18,615  Redistribute aging inventory — ES 350 ZX
```

The SAR 72M figure was cross-checked against `/api/v1/recommendations/order-optimisation`: 364 units
short over the **3-month** horizon × ~198k SAR. The copy was amended to state the horizon explicitly,
so the figure cannot be misread as monthly.

### Tests — 537 total (was 404 at baseline)

- ✅ 52 analytics unit tests (priority model + ranking/aggregation).
- ✅ 18 unit tests for `DecisionFeedService` with in-memory provider fakes: aggregation, ranking,
  per-provider failure isolation, **gap reporting**, **no exception detail leaking into the response**,
  cancellation propagation (not misreported as a data gap), DTO projection, money rounding, narrative.
- ✅ 13 integration tests against the real composition root: reachability, ranking order, payload
  completeness, **every decision links to a screen the web app actually routes**, id uniqueness,
  summary/decision agreement, synthetic-data labelling, determinism across requests, response budget.
- ✅ 22 component tests: loading, populated, empty, error, retry, network failure, **partial-failure**,
  demo labelling, drill-down navigation, severity in words not colour alone.

### Verification

typecheck ✅ · lint ✅ · web build ✅ · 67/67 web ✅ · **470/470 backend** ✅ · architecture 4/4 ✅ ·
no new build warnings. Bundle: CSS 12.61 → 15.57 kB raw (+0.48 kB gzip); JS unchanged at ~324.6 kB.

### Known gaps

- **V3-UC08-008 / D-SUP-1 (supplier delay exposure) is not implemented.** v3 computes it entirely from
  synthetic supplier fixtures in `engine2.js`. The database has **no supplier, purchase-order or
  delivery-performance entity**. Implementing it would mean fabricating supplier performance and
  presenting it to executives as measured — so the rule is omitted and tracked as V3-CONFLICT-9.
- Nav badges (V3-NAV-004) can now be wired from `summary.critical` — moved to S4.
- No visual-regression coverage yet (S12).

### Risks

R-06 addressed: a 5 s response budget is asserted in integration tests. The feed measured well inside
it, but the two slow underlying endpoints remain a S8 concern.

**Next action.** None — slice closed.

## S4 — Identity, roles & authorization · **Analysing**

- **Blocking finding.** The application has **no authentication whatsoever** — verified against the
  live OpenAPI document: 44 endpoints, all anonymous `GET`. ADR-0006 requires a *named human* on every
  decision, so S6 cannot proceed until this lands.
- **Next action.** Author a new ADR covering authentication mechanism, session lifecycle and role
  model. No existing ADR settles it.

## S5 — Recommendation records & write path · **Ready**

- **Blocking finding.** ADR-0006 is **Accepted but entirely unimplemented** — none of its five
  entities exists. This is the largest documentation-vs-code divergence in the repository.
- **Note.** This slice does **not** require identity: its actor is the system, so it may proceed in
  parallel with S4.
- **Next action.** Add `Recommendation` (frozen), `RecStatusEvent` (append-only) and `AuditEvent`
  entities plus an additive migration; build the first write endpoint with idempotency (ADR-0007) and
  optimistic concurrency.

## S6 — Decision Log & human decisions · **Blocked**

- **Blocked on.** S4 (identity — for `decided_by`) and S5 (records — for the append-only substrate).
- **Conflict to resolve on entry.** V3-CONFLICT-1/2 — implement ADR-0006's model, not the prototype's
  mutable/`localStorage` one, while preserving v3's visual design and status vocabulary.
