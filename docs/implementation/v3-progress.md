# V3 Implementation Progress

> Living record of each vertical slice. Statuses: `Not started` · `Analysing` · `Ready` ·
> `Implementing` · `In review` · `Blocked` · `Complete`.
> Last updated: 2026-07-23.

## Summary

| Slice | Title | Priority | Status |
|-------|-------|----------|--------|
| S0 | Dependency & warning hygiene | P1 | Ready |
| **S1** | **Application shell & grouped navigation** | P1 | **Complete** |
| **S2** | **UC8 Executive Decision Cockpit** | P1 | **Implementing** |
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
| After S2 (analytics complete) | **439** | 45 | **484** |

Backend breakdown after S2: `BeeEye.UnitTests` 18 · `BeeEye.Analytics.Tests` **384** (was 332) ·
`BeeEye.ArchitectureTests` 4 · `BeeEye.IntegrationTests` 33. All green, 0 failures, 0 skipped.

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

## S2 — UC8 Executive Decision Cockpit · **Implementing**

- **Requirements.** V3-UC08-001…007.
- **Code.** ✅ `BeeEye.Analytics/Decisions/DecisionPriority.cs` — the multiplicative priority model,
  impact normalisation, severity→due-days, confidence bands and ranked factors, ported faithfully from
  `engine2.js` L511–559 (including `MidpointRounding.AwayFromZero` to match JavaScript's `Math.round`).
  ✅ `BeeEye.Analytics/Decisions/DecisionFeed.cs` — the `Decision` record with derived priority/due/
  confidence/factors, plus `Rank` (totally ordered, reproducible) and `Summarise` (critical,
  low-confidence, due-this-week, opportunity/risk value, demo-data count).
  ⬜ `ExecutiveInsightsReadService` building the six rule candidates. ⬜ endpoint. ⬜ cockpit page.
- **Tests.** ✅ 34 tests for the priority model + 18 for ranking/aggregation — analytics suite
  332 → **384**. Covers multiplicative-vs-additive behaviour, clamping, NaN handling, JS rounding
  parity, tie-breaking determinism, input immutability, negative-impact magnitude and empty feeds.
  ⬜ rule tests, endpoint integration tests, page component tests.
- **Documentation.** ✅ Traceability rows recorded.
- **Verification so far.** 384/384 analytics ✅ · **439/439 full backend regression** ✅ · no new build
  warnings ✅.
- **Design note.** `DecisionFeed` deliberately holds **no** module-specific rules. The six v3 rules read
  from several bounded contexts, so they are assembled by the ExecutiveInsights read service and passed
  in as candidates — preserving module isolation (CLAUDE.md rule 3) and keeping ranking purely testable.
- **Known gaps.** The six rule builders, the endpoint and the UI remain.
- **Risks.** R-06 — the cockpit aggregates six modules including the 669 ms after-sales endpoint;
  budget assertions required.
- **Next action.** Implement `DecisionFeed` composing UC1/3/4/5/6/7 results, then the read service and
  endpoint, then the page with loading/empty/error/populated states.

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
