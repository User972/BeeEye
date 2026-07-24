# V3 Implementation Progress

> Living record of each vertical slice. Statuses: `Not started` · `Analysing` · `Ready` ·
> `Implementing` · `In review` · `Blocked` · `Complete`.
> Last updated: 2026-07-24.

## Summary

| Slice | Title | Priority | Status |
|-------|-------|----------|--------|
| **S0** | **Dependency & warning hygiene** | P1 | **Complete** |
| **S1** | **Application shell & grouped navigation** | P1 | **Complete** |
| **S2** | **UC8 Executive Decision Cockpit** | P1 | **Complete** |
| **S3** | **Explainability drawer & AI label system** | P1 | **Complete** |
| **S4** | **Identity, roles & authorization** | P0 | **Complete** (backend) |
| **S4b** | **SPA sign-in flow (MSAL, 401/refresh, route gate)** | P0 | **Complete** |
| **S5** | **Recommendation records & write path** | P0 | **Complete** |
| **S6** | **Decision Log & human decisions** | P1 | **Complete** |
| **S7** | **Data Health, Lineage & Settings** | P2 | **Complete** |
| S8 | Intelligence-screen alignment & performance | P2 | Not started |
| S9 | Persona, accent, density | P3 | Not started |
| S10 | Ask Decision Intelligence | P2 | Not started |
| S11 | Ingestion, Reports, Methodology, Integration | P3 | Not started |
| **S12** | **E2E, visual, a11y, coverage hardening** | P1 | **Complete** |

## Test totals

| Milestone | Backend | Web | Total |
|-----------|---------|-----|-------|
| Baseline | 387 | 17 | **404** |
| After S1 | 387 | 45 | **432** |
| After S2 (analytics complete) | 439 | 45 | 484 |
| After S2 (complete) | 470 | 67 | 537 |
| **After S4 + S5** | **615** | **67** | **682** |
| **After S6** | **814** | **116** | **930** |
| **After S3** | **885** | **200** | **1085** |
| **After S4b** | **885** | **234** | **1119** |
| **After S12** | **885** | **244** | **1129** |
| **After S7** | **982** | **276** | **1258** |

S12 also introduces a **Playwright** suite (E2E journeys · route-level a11y · visual regression) that runs
in its own CI job and is counted separately from the vitest totals above; the vitest column gained the
10 component-level `vitest-axe` tests (234 → 244).

Backend breakdown after S5: `BeeEye.UnitTests` **129** (was 18) · `BeeEye.Analytics.Tests` **384**
(was 332) · `BeeEye.ArchitectureTests` 4 · `BeeEye.IntegrationTests` **98** (was 33).
Web: 67 (was 17). All green, 0 failures, 0 skipped.

Backend breakdown after S6: `BeeEye.UnitTests` **259** (was 129) · `BeeEye.Analytics.Tests` 384
(unchanged — S6 touches no formula) · `BeeEye.ArchitectureTests` **5** (was 4) ·
`BeeEye.IntegrationTests` **166** (was 98). Web: **116** (was 67).
All green, 0 failures, 0 skipped.

Backend breakdown after S3: `BeeEye.UnitTests` **297** (was 259) · `BeeEye.Analytics.Tests` 384
(unchanged — S3 changes no formula, and the `engine.js` parity tests are untouched) ·
`BeeEye.ArchitectureTests` **6** (was 5) · `BeeEye.IntegrationTests` **198** (was 166).
Web: **200** (was 116). All green, 0 failures, 0 skipped.

---

## S0 — Dependency & warning hygiene · **Complete**

- **Requirements.** V3-QA-005, V3-QA-006.
- **Why it ran now.** The build carried 18 `NU1903` advisories and 5 `CS8604` warnings, which made
  "this slice introduced no new warnings" unverifiable — the claim every subsequent slice's
  verification section makes. Clearing them first turns that claim into an assertion.

### What changed

- **`Microsoft.OpenApi` pinned to 2.11.0.** It arrives transitively via `Microsoft.AspNetCore.OpenApi`
  10.0.0, which resolves 2.0.0. GHSA-v5pm-xwqc-g5wc (circular schema references terminate parsing) is
  patched in 2.7.5. Held on the **2.x line** rather than moved to 3.x: 3.x is a new major the ASP.NET
  Core package is not built against, and the advisory does not require crossing it.
- **`System.Security.Cryptography.Xml` pinned to 10.0.10.** It arrives at 9.0.0 through the EF Core
  design-time graph — one major behind this solution's target framework. 10.0.x clears all seven
  advisories reported against 9.0.0.
- **5 `CS8604` warnings cleared** in `ConfigurationDemand.cs` (3) and `ForecastingReadService.cs` (2).
  All five are `Min`/`Max` over a reference-typed selector, which the BCL declares nullable because it
  must answer for an empty sequence. Every call site is already guarded by an emptiness check, or
  operates on a LINQ grouping that holds at least one row by construction — so the fix is a
  null-forgiving operator with a comment naming the guard, not a defensive null check for a case that
  cannot arise.

### Verification

`dotnet build BeeEye.slnx` → **0 warnings, 0 errors** (was 23 warnings). Test totals unchanged at
**814/814 backend** — 384 analytics · 259 unit · 5 architecture · 166 integration. No formula, entity
or contract changed.

**Next action.** None — slice closed.

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

## S3 — Explainability drawer & AI label system · **Complete**

- **Requirements.** V3-DS-002, V3-DS-006, V3-DS-007 (extended), V3-UC0x-002.
- **Outcome.** Every number, forecast and recommendation the platform shows can now answer "why?" in
  one consistent panel, on all nine screens.

### Architecture — the `IExplainabilityProvider` seam

The drawer spans eight contexts, and the boundary rules forbid a module referencing another module.
The same answer S2 found applies: a **published contract** in
`BeeEye.Analytics/Explainability/IExplainabilityProvider.cs`. Each context explains its own output;
the `Predictions` module injects `IEnumerable<IExplainabilityProvider>` and only routes, aggregates
and reports gaps.

- Every explanation stays with the context that owns the figure it explains.
- `Predictions` references **no module** — only `BeeEye.Analytics` and `BeeEye.Persistence`. A sixth
  architecture test asserts every provider lives in a `BeeEye.Modules.*` assembly and gets its
  contract from `BeeEye.Analytics`, and that there are exactly **eight** of them: a drop to seven
  means a screen quietly lost the ability to explain itself.
- **A kind is claimed by exactly one provider, and a duplicate claim fails at start-up.** The service
  is resolved once during endpoint mapping precisely so the check runs at boot. Two providers on one
  kind means whichever answers depends on dependency-injection registration order — the worst kind of
  bug to discover on the request path.

### The two conflicts this slice resolves

**V3-CONFLICT-8 — seven labels or eight.** `docs/wireframes-v3/README.md` documented seven AI output
labels and omitted `Data Quality`; `engine2.js`'s `LABELS` table (L28–37) defines eight. **The code
was the source of truth**: all eight ship in `components/ui/AiLabel.tsx`, and the README and the
design inventory were corrected in the same commit rather than left disagreeing with the code. The
completeness of the map is asserted by reflection over the C# enum *and* by an exhaustive component
test, so a ninth label cannot be added without a chip.

**"Was this useful?" — the conflict the plan did not name.** v3's drawer ends with four feedback
buttons under a caption saying the answer "is recorded in the analytics platform only". It is
recorded nowhere: `explainFeedback()` writes to component state and the answer dies on reload. That
is exactly the pattern ADR-0006 rejects (V3-GOV-011), and a control that silently discards input is
worse than no control — it spends a reader's goodwill and returns nothing.

**Resolved by persisting it properly.** The S6 machinery made this cheap: the idempotency filter, the
`IIdempotencyStore` seam and the permission catalogue all existed. `ExplainabilityFeedback` is a
sixteenth table, append-only, attributed to a stable subject id, written behind
`RequirePermission` + `.WithIdempotency()`. The caption stays — and is now true.

### Backend

- **Contract** (`BeeEye.Analytics/Explainability/`): `IExplainabilityProvider`, the `Explanation`
  record and its parts, the `OutputLabel` / `LineageKind` / `ImpactTone` / `ConfidenceBand` enums with
  their wire-key maps, and `ExplanationFormat` — invariant money/date formatting matching
  `src/web/src/lib/format.ts` so a figure reads identically on a screen and in the drawer explaining
  it.
- **Eight providers**, one per live context: `OrderExplainabilityProvider` (UC1),
  `ForecastExplainabilityProvider` (UC2), `ConfigurationExplainabilityProvider` (UC3),
  `ProcurementExplainabilityProvider` (UC4), `InventoryExplainabilityProvider` (UC5),
  `AfterSalesExplainabilityProvider` (UC6), `SparePartsExplainabilityProvider` (UC7) and
  `CockpitExplainabilityProvider` (UC8, claiming both `decision` and `brief`).
- **`GET /api/v1/predictions/explain?kind=&ref=`** — 400 on an unknown kind, *listing the kinds
  actually registered*; 404 on a known kind with an unknown reference; **200 with a reported gap**
  when a provider throws. `Predictions` moves from `scaffolded` to `operational`.
- **`POST /api/v1/predictions/explain/feedback`** — `RequirePermission` +
  `.WithIdempotency()`, body implementing `IIdempotentPayload`.
- **New permission** `explanation-feedback.submit`, in `All` **and** `StateChanging`, granted to
  Executive and Analyst.
- **Migration `ExplainabilityFeedback`** — one `CreateTable` and two `CreateIndex`; the `Down` drops
  only the new table. The fifteen pre-existing tables are asserted untouched against
  `information_schema`, not by reading the migration and hoping.

### Six decisions worth recording

1. **Confidence is nullable, and an absent band stays absent.** v3 defaults a missing confidence to
   `Medium`. That is an invented assertion wearing the costume of a default, and it is not reproduced:
   `Confidence is null` survives the whole pipeline, the section is omitted, and a test asserts the
   word "Medium" appears nowhere in a drawer whose payload carries no band.
2. **`IsDemoData` is an explicit flag, not something derived from lineage.** Inference would be a rule
   two places must agree on forever, and the day they disagree is the day a synthetic figure loses its
   label in front of an executive. `Demo` is also **not exclusive**: a demo-derived forecast carries
   `Label = Forecast`, `IsDemoData = true` and a `Demo` lineage node, and a test pins all three.
3. **`ImpactTile.Value` is a pre-formatted string; `Tone` is an enum, never a colour.** The server
   knows whether a figure is money, a count or a percentage; the browser would have to be told, and
   the first client that forgot would render `72095231.5` to an executive. A colour in a contract is a
   colour nobody can theme.
4. **Null and failure are different answers.** A provider returning `null` means "no such subject" →
   404. A provider that *fails* throws → the service reports a **gap** and answers 200. Collapsing
   them would tell a user that a figure carries no explanation when the truth is that the data could
   not be reached.
5. **No new read permission.** The explain route uses `executive-cockpit.view`. A role that could read
   a number but not its basis would be strictly worse informed than one that could read neither, and
   no ADMC role wants that.
6. **The evidence chart appears only where a screen already has one to give it.** UC2's back-test
   (actual vs fitted) and UC5's additive risk factors, plus UC7's usage history. Everywhere else the
   section is **absent from the DOM**, and the integration tests assert the null rather than merely
   not asserting the chart — which is what stops someone helpfully filling it later.

### Honest assumptions, promoted out of hiding

UC6's assumed **SAR 350/hour** labour rate lived in an evidence string in S2, where nobody reads it.
It is now an `Assumption`, stated in the section a reader goes to for exactly this. UC4 states the
missing supplier feed in the same place, and carries a `Demo`-kind lineage chip reading "Supplier & PO
history — not integrated" (V3-CONFLICT-9), so nobody can assume its safety stock accounts for
supplier reliability. UC3 states that its stockout suspicion is inferred from the absence of sales
*and* stock, and is not confirmed against a stock-movement history the platform does not hold.

### Frontend

- **`AiLabel`** — all eight labels, text and icon from v3 verbatim, **colour in `components.css`** as
  `.ai-label--{kind}` rather than inline. The label text is always rendered: never colour alone. An
  unrecognised key falls back to `recommendation` (v3's behaviour) **and warns in development** — a
  silent fallback would render a confident "Recommendation" chip on something that is not one.
- The three hand-rolled `badge badge--demo` spans in `executive-cockpit.tsx` and `decision-log.tsx`
  (two) now use `<AiLabel kind="demo" />`. Their tests were updated from "Demo data" to v3's exact
  **"Demo Data"** — the label is the contract, so the test moved, not the label.
- **`ExplainabilityDrawer`** — v3's eleven sections in v3's order, each a real
  `<section aria-labelledby>` and each **absent from the DOM** when its data is absent. Built on the
  shared `Drawer` with a `.drawer--explain` modifier carrying v3's 474px / 94vw, so every other drawer
  keeps the geometry it was designed at.
- Drivers are capped at 8 behind a `Show all (n)` disclosure. An unbounded list turns the panel into a
  scroll trap, and UC5's factor list is already five long before anything is added to it.
- All six states: loading (the drawer opens *immediately* and fills in), no-recorded-explanation,
  error with the server's own `detail`, network failure with actionable text, **partial** (what
  arrived plus what is missing) and permission-denied.
- Feedback submits with one idempotency key per intent, held in a ref that survives TanStack Query's
  retry, and **no optimistic update** — the same reasoning S6 recorded.

### The design correction testing surfaced

The shared `Drawer` had **two** defects that only appear when two drawers are open at once, which is
exactly what the Decision Log now does:

1. **Every drawer attached its own `window` keydown listener**, so one Escape closed *both* — losing
   the decision the user was reading in order to dismiss the explanation of it. The same applied to
   Tab: two live focus traps fighting over one keypress, resolved by listener registration order.
   Fixed with a module-level drawer stack; only the topmost entry acts.
2. **`onClose` sat in the focus effect's dependency array.** It is almost always an inline arrow, so
   its identity changes on every parent render — and the effect's teardown *restores focus*. A
   re-render while the drawer was open therefore yanked focus back to the invoking control and then
   back into the panel. Invisible with one drawer; with two, it left focus in the wrong panel. Held in
   a ref now, with the effect depending on `open` alone.

Neither was found by reading the code. The stacked-drawer test found both, and both have regression
tests: Escape closes only the top, the second Escape closes the one beneath, focus lands **inside**
the drawer beneath, and Tab stays trapped in the top.

### Tests — 1085 total (was 930)

- **38 backend unit tests added** (259 → 297): the label, lineage and tone maps complete **by
  reflection** over their enums, with an unmapped value throwing rather than falling back; the eight
  v3 keys asserted in order; section-presence in both directions; confidence absent surviving the
  whole pipeline with no band invented; demo-ness stated rather than inferred; tones projected as keys
  and never as `var(--…)`; invariant money and dates under a comma-decimal culture built by cloning
  `InvariantCulture`; and the aggregation service with fakes — one claimant answers, a
  non-claimant is never called, an unknown kind is not a not-found, a throwing provider becomes a
  **gap** carrying no exception detail, and cancellation propagates rather than being misreported as a
  data gap.
- **32 integration tests added** (166 → 198): one per subject kind against the real composition root,
  each asserting a complete payload, a label from the eight, non-empty lineage and every money value
  an invariantly-parsable string; the unknown-kind 400 naming all nine registered kinds; the
  unknown-reference 404; anonymous and wrong-role refusals; **a failing provider yielding a gap and a
  200 with no exception detail anywhere in the body**; feedback recorded, read back, replayed
  identically under one key, refused at 422 for a replayed key with a changed verdict, refused for a
  missing or malformed key, refused unauthenticated **even in the relaxed read posture**, and
  appending rather than overwriting when someone changes their mind; **no `DELETE` under
  `/predictions/explain` in the served OpenAPI document**; the sixteenth table created and the fifteen
  existing ones unchanged; and response-time budgets.
- **1 architecture test added** (5 → 6): eight providers, each in a module assembly, each getting its
  contract from `BeeEye.Analytics`.
- **84 web tests added** (116 → 200): all eight labels with their exact text, modifier class and icon,
  the unknown-key fallback *and* its warning; every one of the eleven sections asserted **absent**
  when its data is absent; confidence with and without a percentage and the "Medium" prohibition;
  drivers capped with a working disclosure; all six states; the feedback round trip including a
  server 4xx rendered inline and the absence of an optimistic update; the caveat; accessible names on
  every trigger; focus returning to the invoking button; the stacked-drawer chain; and one wiring case
  per screen asserting the drawer opens for **that** subject reference — the failure a hand-check
  never finds is a button wired to the wrong row.

### Explicitly not implemented

- **Live AI narration (V3-PLAT-002, S10).** The drawer renders the deterministic engine's own
  explanation. The seam is the `IExplainabilityProvider` contract and nothing in this slice crosses
  it; **no model call ships in S3** (ADR 0006 §2.6, `overview.md` §8 — *GenAI narrates, never
  decides*).
- **Self-hosted fonts (V3-DS-003 / V3-CONFLICT-3).** S8.
- **The historical-evidence chart for UC1, UC3, UC4 and UC6.** Those screens have no chart for the
  drawer to reuse, and a placeholder would imply evidence that is not being shown.
- **Feedback influencing anything.** No model consumes the table; the response and the caption both
  say so, and a test asserts the caption.
- **Explaining a *tuned* scenario.** UC1 and UC4 explain the default planning scenario. Explaining an
  analyst's tuned figure needs the scenario inside the subject reference, which is deferred rather
  than half-done — see the gap below.

### One thing S3 found that was not S3's

`src/web/openapi/openapi.json` — the committed contract snapshot the `openapi` CI job diffs against —
was **stale by nine routes**. S6 added the whole decision workflow and `GET /identity/me`, and removed
the Identity module-info route, without re-exporting it. Regenerating for this slice's two new
endpoints picked all of that up: 47 paths → 57. The snapshot is now current, and the drift gate is
protecting something again.

### Known gaps

- **Scenario-aware subject references (UC1, UC4, UC7).** The screens let an analyst tune horizon,
  target cover, service level and review period; the drawer explains the *default* scenario. Where the
  analyst has changed a control, the explanation is therefore for a neighbouring figure rather than the
  one on screen. Tracked as tech debt (TD-5) rather than papered over.
- **UC6/UC7 remain the two slow paths (V3-PERF-001).** Explaining a part goes through the same
  per-request recomputation the S8 slice exists to fix, so the provider inherits that cost. Both
  explain endpoints are held to the **same 5 s budget as everything else** rather than given a wider
  one — a wider budget would hide the fix S8 is supposed to prove.
- **The "partial" response shape.** Because exactly one provider claims each kind today, the reachable
  partial state is *gap present, explanation absent*. The contract permits gap-and-explanation
  together and the drawer renders it; that variant is covered by a component test against a mocked
  response rather than an integration test, and will become reachable when a kind is served by more
  than one context.
- **No visual-regression coverage** (S12), so the 474px geometry and the eleven-section layout are
  asserted structurally, not pixel-wise.

### Verification

typecheck ✅ · lint ✅ · web build ✅ · **200/200 web** ✅ · **885/885 backend** ✅ · architecture 6/6 ✅ ·
**`dotnet build` 0 warnings** · the **384 `engine.js` parity tests unchanged and green** — S3 changes
no formula. Bundle: a shared 23.86 kB explainability chunk (8.40 kB gzip) loaded only by screens that
use it; the index chunk is unchanged at ~325 kB.

**Next action.** None — slice closed. Scenario-aware references and the UC6/UC7 performance work are
S8; live narration is S10.

## S4 — Identity, roles & authorization · **Complete** (backend)

- **Requirements.** V3-AUTH-001/002/003/004.
- **Decision record.** [ADR 0008 — Authentication & Permission-Based Authorization](../adr/0008-authentication-and-authorization.md),
  ratifying the model `docs/architecture/security-threat-model.md` §2–§3 already specified but which
  no ADR had made binding. The application previously had **no authentication whatsoever** — 44
  endpoints, all anonymous.

### What changed

- `Permissions` — a 25-permission catalogue with an explicit `StateChanging` subset.
- `RolePermissions` — Executive / Analyst / IT-Admin mapping. **The only place a role is interpreted**:
  re-cutting ADMC's roles changes this table and no endpoint.
- Entra ID JWT validation: exact issuer, pinned audience, asymmetric algorithms only (so `alg: none`
  and symmetric downgrades are rejected), and lifetime validation with clock skew **capped at two
  minutes regardless of configuration** — a generous skew is a replay window.
- `LocalDevAuthenticationHandler` — issues a configured local principal so the stack runs with no
  Entra tenant.
- `PermissionAuthorizationHandler` — expands roles to permissions; nothing tests a role name.
- `RequireReadPermission` / `RequirePermission` applied across all eight live modules.

### Authorization is always evaluated

There is deliberately **no "authorization disabled" mode** — such a mode inevitably reaches
production. The development provider changes *who you are*, never *whether you are checked*.

The rollout flag `Auth:RequireAuthenticatedReads` relaxes **reads only**, defaults off in Development
and on everywhere else, and is implemented in exactly one place (the policy registration).
`RequireReadPermission` **throws at start-up** if handed a state-changing permission, so a write can
never be declared in a form that configuration could relax.

### The three guards on the development provider

1. Registration gated on `IsDevelopment()`.
2. Explicit `Auth:Provider` opt-in; not the deployed default.
3. A start-up assertion that **throws and aborts boot** if selected outside Development.

Guard 3 fails the process rather than falling back: a misconfigured deployment does not quietly run
with a fake identity — it does not run at all.

### Tests — 82 added

- **64 unit tests**: catalogue completeness by reflection (adding a permission without registering it
  fails the build's tests), `resource.action` shape, state-changing classification, every role
  granting only known permissions, **every permission reachable by some role** (an unreachable
  permission is a dead endpoint), unknown roles ignored rather than failing the request, and the full
  role→permission matrix asserted against the threat model.
- **Segregation of duties is asserted, not assumed**: no role holds both sides of any author/approve
  pair, and the IT-Admin holds no business-approval authority.
- **18 integration tests**: 401 anonymous, 401 malformed token, 403 wrong role, 403 no roles, 403
  unmapped role, 200 correct role, per-endpoint checks across all eight modules, health probes
  reachable unauthenticated, the Development read posture preserved, and **three start-up assertions**
  (dev provider in Production, Entra without authority, Entra without audience) each aborting the host.

### Known gaps

- **The SPA sign-in flow (MSAL, token acquisition, refresh, 401 handling) is not implemented.** The
  backend is complete and enforced, but the browser cannot yet obtain a token — which is why
  `RequireAuthenticatedReads` defaults off in Development. It is the named removal condition for that
  flag.
- Data-scope filtering (*which rows*, threat model §3.3) is a separate concern and is **not** delivered
  here.

**Next action.** None for the backend — slice closed. SPA sign-in is distinct work.

## S5 — Recommendation records & the write path · **Complete**

- **Requirements.** V3-GOV-002/003/005, V3-API-001/002/003. V3-GOV-011 and V3-GOV-012 **rejected** as
  designed (below).
- Closes the largest documentation-vs-code divergence in the repository: ADR 0006 was Accepted with
  **no** implementing code.

### What changed

- `RecommendationLifecycle` — the ADR 0006 §3 state machine in the shared kernel: 9 states, 10 edges,
  three guards, and typed refusal reasons with safe, non-technical explanations. Pure and
  deterministic, so every edge and guard is exhaustively testable.
- `Recommendation` entity — frozen engine output plus provenance (`RulesetVersion`, `DatasetVersion`,
  `AnalysisDate`), `CurrentStatus` as a projection, `ValidUntilUtc`, `SupersededByRecommendationId`,
  and an `xmin` row-version concurrency token.
- `RecommendationStatusEvent` — the append-only log. `Restrict`, not `Cascade`, on the foreign key:
  the audit trail must survive any attempt to remove its subject.
- Migration `RecommendationRecords` — **additive only**; the nine existing entities are untouched.
- `POST /api/v1/recommendations/records/generate` — the platform's **first write endpoint**.
- `GET /api/v1/recommendations/records` — paged, status-filterable, page size clamped.

### The three properties the ADR exists to guarantee

1. **The original is immutable.** A record is inserted once and never updated by this service. A later
   run supersedes; it does not overwrite.
2. **Status lives in an append-only log.** Every record opens with a `Generated` event attributed to
   `system` — the engine acted, not a person, and attributing it to a human would corrupt the
   accountability trail.
3. **Generation is idempotent.** The key is `ruleset | analysisDate | ruleId | subject`, enforced by a
   unique index. The existence check is an optimisation; the index is the guarantee — a lost race
   surfaces as SQLSTATE 23505 and is treated as a successful no-op.

The analysis context is **anchored to the data** (latest sales month, newest ingestion checksum), not
the wall clock, so the same database always yields the same idempotency keys.

### Explicitly rejected, per ADR 0006

- **V3-GOV-011** — `localStorage` persistence. The v3 prototype still stores decisions in the browser;
  ADR 0006's `Supersedes` field names exactly that behaviour.
- **V3-GOV-012** — hard delete. There is **no delete path**; terminal states end a record's life.

### One design correction found by testing

ADR 0006 expresses "expiry is suspended once under review" as a *missing* edge, so the dedicated guard
was unreachable and a generic "not a valid next step" was returned instead. Since the expiry job
legitimately attempts that transition on every unreviewed record, the edge was added and left
permanently refused by the guard — the caller now gets "someone owns it" rather than a generic
refusal. The transition matrix is unchanged.

### Tests — 63 added

- **29 unit tests** for the lifecycle, including a **full 9×9 transition matrix** asserted against an
  independently written edge set, every state reachable from `Generated`, no self-transitions, every
  terminal state refusing everything, all three guards, and every refusal carrying a safe explanation
  that leaks no type names.
- **23 integration tests**: authorization (an **Executive cannot generate** — segregation of duties),
  the write refused unauthenticated even in the relaxed read posture, idempotency across repeated
  runs, **four concurrent runs creating no duplicates**, unique keys, full provenance on every record,
  the opening status event attributed to `system`, projected status agreeing with the log, validity
  windows, ordering, status filtering, an unknown status returning 400 listing the valid values, and
  page size clamped so an unbounded set never reaches the browser.

### Known gaps

- `ManagementDecision`, `ApprovalStep` and `ActionOutcome` (V3-GOV-004) are S6.
- `AuditEvent` (V3-API-004) is not yet implemented; the status-event log currently carries the trail.

**Next action.** None — slice closed. S6 is now unblocked.

## S6 — Decision Log & human decisions · **Complete**

- **Requirements.** V3-GOV-001/004/006/007, V3-API-002/005, V3-AUTH-004, V3-DS-007, V3-PLAT-007.
- **Unblocked by** S4 (identity, so a decision can name a human) and S5 (the frozen record and the
  append-only status log).

### The conflict this slice resolves

**V3-CONFLICT-1 and V3-CONFLICT-2 are closed.** The v3 prototype models decisions as mutable rows in
`localStorage` with a free nine-value status dropdown and a delete button on every row. ADR-0006
rejects precisely that shape — "single mutable record" is its rejected Option B, and its `Supersedes`
field names the prototype's `localStorage` behaviour. S6 keeps v3's **visual design and status
vocabulary** and replaces its **data model and interaction semantics**:

| v3 prototype | S6 |
|---|---|
| `localStorage` array of actions | `ManagementDecision` rows keyed to a frozen `Recommendation` |
| Free `<select>` over 9 statuses | Guard-validated transition **actions**; the server publishes the legal next steps per row and the screen renders only those |
| Delete button (`a.del`) | **No delete path, at any layer.** Rejection is the terminal state that ends a record's life while keeping *why* |
| `updateAction(id, field, value)` in-place edit | Append a `ManagementDecision` / `ApprovalStep` / `ActionOutcome` / status event |
| `logAllDecisions()` bulk-inserts from the cockpit feed | Bulk **claim** of records the engine already generated; nothing is fabricated in the browser |

**Status vocabulary.** v3's labels map onto ADR-0006's nine states: `New`→`Generated`,
`Under review`→`UnderReview`, `Accepted`→`Accepted` **and** `AcceptedModified` (rendered "Accepted"
and "Accepted with modification"), `In progress`→`Implemented`, `Completed`→`OutcomeRecorded`, plus
`Rejected`, `Superseded` and `Expired`.

**v3's `Assigned` and `Snoozed` are dropped.** Neither has an ADR-0006 counterpart, and adding them
would mean two sources of truth for where a record stands. Ownership is expressed by the
recommendation's `OwnerRole` plus the subject id of whoever claimed it — both already recorded, both
already visible — and the governed lifecycle has no defer/snooze state. Recorded in
`v3-design-traceability.md` under V3-GOV-006.

### Backend

- **Entities** `ManagementDecision`, `ApprovalStep`, `ActionOutcome` (V3-GOV-004) and
  `IdempotencyRecord`, with the additive `ManagementDecisions` migration. The eleven pre-existing
  tables are untouched — asserted by an integration test that reads `information_schema`, not by
  reading the migration and hoping.
- **`RecommendationTransitionService`** — the only writer of lifecycle state anywhere in the platform
  (ADR-0006 §6). It validates via `RecommendationLifecycle` and nothing else, then appends the status
  event and updates the `CurrentStatus` projection **in one `SaveChangesAsync`**, so the cached column
  can never disagree with the log that is the source of truth.
- **`DecisionService`** — the human rules above the state machine: claim, accept,
  accept-with-modification, reject, sign-off, mark implemented, record outcome.
- **`Idempotency-Key` (ADR-0007 §2.1)** built once and reusably in `BeeEye.Shared.Web/Idempotency`,
  applied to all seven writes via `.WithIdempotency()`. The effect and the key row share one
  transaction, so they commit or roll back together.
- **`GET /api/v1/identity/me`** — anonymous-friendly, answering `isAuthenticated: false` rather than
  401, so a signed-out SPA renders its own state from a successful response.
- `DecisionsAndOutcomes` and `Identity` move from `scaffolded` to `operational`.

### Three independent layers of segregation of duties

1. **Permission separation** — `recommendation.generate` is the Analyst's, `recommendation.approve`
   the Executive's, and `RolePermissions.AuthorApprovePairs` guarantees no role holds both.
2. **Actor separation** — an `ApprovalStep` may not be signed off by the same subject id that decided
   (or opened) it. `SubjectIds.Same` compares **ordinally on trimmed values**, so neither a stray
   space nor a case difference becomes a one-character bypass; both are tested.
3. **The outcome recorder may be the same person**, deliberately. Measuring a realised result is
   observation, not a second approval, and requiring a third party would simply mean outcomes never
   get recorded — losing the one signal that says whether the recommendations were any good. The new
   `decision-outcome.record` permission is therefore *not* part of an author/approve pair, and a test
   asserts that so a later reader does not "fix" it.

### Two decisions taken under ambiguity

- **`Idempotency-Key` is strictly required, not optional-but-honoured.** Optional leaves the guarantee
  to whichever client remembers to opt in, and the first one that forgets is the one that double-books
  a decision worth millions of SAR. A missing header fails at the edge, where the fix is obvious.
- **Claiming seeds one `ApprovalStep`** from the recommendation's owner role. Seeding at claim time
  rather than at accept time makes the ADR-0006 §3 supersession guard real from the moment a human
  takes ownership, so a fresh analysis run cannot erase a decision mid-flight.

### The design correction testing surfaced

The `Idempotency-Key` fingerprint serialised its payloads through the `IIdempotentPayload` interface.
System.Text.Json serialises against the **declared** type, so every request body hashed to `{}` — and
two genuinely different requests replaying one key would have been indistinguishable, returning the
first request's answer for the second request's intent. Reading the code did not find this; the
integration test that replays a key with a changed body did. Payloads are now serialised by runtime
type, and the comment at that line records why.

The lesson generalises: an idempotency fingerprint has no observable behaviour until two *different*
requests share a key, so it is exactly the kind of code that looks correct and is not.

### Frontend

- `apiPost` with an `Idempotency-Key` **minted once per user intent** and held in a ref that survives
  TanStack Query's retries — a fresh key per retry would defeat the whole mechanism. Refusals (4xx)
  are not retried at all; only transport failures are, which is what the key makes safe.
- No optimistic updates anywhere: a guard may refuse server-side, and showing a state the server
  rejected is precisely the failure mode ADR-0006 exists to prevent.
- `/decisions` reproducing v3's layout — status chip row with counts and v3's colour tokens, the
  `gavel` governance banner, row cards with a status-coloured left border — with **actions in place of
  the dropdown**. Only transitions the server published for that row are rendered; everything else is
  *absent*, not present-and-disabled, and a single line explains where a control is hidden for
  permission reasons.
- The `Drawer` primitive gained a **focus trap and focus restoration** (V3-DS-007, brought forward
  from S3): the detail drawer is a modal, and both were missing from v3 and from the app.
- Drawer footers on the UC5/UC6/UC7 screens route into the log (V3-GOV-007). Where no persisted record
  exists for what the drawer is showing, the footer **says so** rather than offering a control that
  would do nothing.

### Tests — 930 total (was 682)

- **130 backend unit tests added** (129 → 259): the modification delta including the discount band at
  `-0.1 / 0 / 10 / 20 / 20.01`, `from == to`, negative quantities, `to == 0`, decimal precision through
  a serialise/deserialise round trip and invariant formatting under a comma-decimal culture;
  self-approval including the whitespace and case bypasses; **every action the service exposes asserted
  against `RecommendationLifecycle`** rather than a hand-written expectation table; the
  approval-in-flight guard blocking supersession and releasing it; the fingerprint's order-independence
  and its discrimination; key validation.
- **68 integration tests added**: the full lifecycle with the status log asserted row-by-row and
  actor-by-actor; **every column of the frozen recommendation snapshotted before and after a decision**
  and compared; cross-role attempts; self-approval over the wire; four concurrent claims yielding
  exactly one decision; a replayed key returning the identical body; a replayed key with a changed body
  returning 422; missing and malformed keys; a forced failure between the effect and the key commit
  leaving **neither** persisted; the four new tables present and the eleven old ones unchanged; and
  **no `DELETE` under `/decisions` in the served OpenAPI document**.
- **49 web tests added** (67 → 116): loading, empty, no-match, error, permission-denied and conflict
  states; only-permitted transitions asserted as *absent* rather than disabled; reject blocked
  client-side and the server's 400 rendered when it is not; a 25% discount refused with the 0–20%
  message; a 409 surfacing the server's explanation and triggering a refetch; honest partial-success
  reporting on bulk claim; the drawer showing the original beside the decision and returning focus on
  close; CSV escaping against commas, quotes, newlines, non-ASCII and formula-injection prefixes.

### Two S5 tests corrected

`Every_new_record_starts_in_the_generated_state` and `Records_can_be_filtered_by_status` asserted that
*every* record in the database is `Generated` and that *no* record is `Rejected`. Both held only while
nothing could move a record, and both were testing the fixture rather than the behaviour. They now
assert what they meant: an **untouched** record is `Generated`, and a status filter returns rows
**carrying that status**.

### Explicitly not implemented

- **The expiry and supersession jobs.** The transition service supports both transitions and both
  guards are exercised, but no background job or cross-module trigger ships here: supersession is
  raised by a *generation* run in the `Recommendations` module and needs a published contract, which is
  a later slice.
- **`AuditEvent` (V3-API-004).** The status-event log remains the trail. Tracked as TD-4.
- **Notifications on decision events.**
- **Structured recommendation parameters.** A modification's `from` is verified against the engine's
  value only where the frozen action text states it; where it does not, the check is skipped rather
  than guessed at. Tracked as TD-3.

### Verification

typecheck ✅ · lint ✅ · web build ✅ · 116/116 web ✅ · **814/814 backend** ✅ · architecture 5/5 ✅ ·
no new build warnings. Bundle: the Decision Log is its own 16.58 kB chunk (5.36 kB gzip); the shared
index chunk is unchanged at ~325 kB.

**Next action.** None — slice closed. The expiry/supersession jobs and `AuditEvent` are separate work.

## S4b — SPA sign-in flow (MSAL) · **Complete**

- **Requirements.** `V3-AUTH-001` — its *named removal condition* (ADR 0008 §2.4, §Consequences). The
  browser must obtain and refresh an Entra access token, attach it to every API call, recover from a
  401, and sign out, so a deployed host can run with `Auth:RequireAuthenticatedReads = true` instead of
  serving reads anonymously.
- **Outcome.** The backend was already complete and enforced (S4); this slice makes the browser produce
  the tokens. No backend code changed — S4b is web + docs only.
- **Unblocked by** S4 (identity, roles, `/identity/me`, JWT validation, LocalDev provider).

### What changed

An **auth-provider abstraction with two implementations, selected by configuration, not by environment
guesswork** (ADR 0008 §2.4):

- **`local` mode** — the default when `VITE_AAD_CLIENT_ID` is absent. No bearer, no gate, no MSAL; the
  app renders anonymously exactly as it did before this slice, mirroring the backend's LocalDev posture.
  `npm run dev` still needs no Entra tenant.
- **`entra` mode** — real MSAL (`@azure/msal-browser` + `@azure/msal-react`, pinned exact). PKCE
  authorization-code flow, redirect as the primary interaction, `sessionStorage` cache.

Mode is chosen by a single `VITE_AUTH_MODE` (`entra` | `local`), defaulting to `local` when no client
id is set. A **production `vite build` fails fast** (`beeeye-auth-build-guard`) when the app is meant
for Entra but is missing any of `VITE_AAD_CLIENT_ID` / `VITE_AAD_AUTHORITY` / `VITE_AAD_API_SCOPE`, so a
deployment can never boot into accidental anonymous mode. The guard reuses the runtime
`resolveAuthConfig`, so build-time and runtime enforcement cannot diverge.

### The single subtlest edge case — the idempotency key across a 401 replay

The transport layer (`lib/api/client.ts`) stays free of React and MSAL. It gained a token-provider
seam and recovers from **exactly one** 401 by refreshing the token and replaying the *identical*
request. The key resolves **once**, before the first attempt, and both the original POST and the replay
carry the same `Idempotency-Key` (ADR 0007 §2.1) — minting a fresh key on the retry would let the
server record a second, unrelated decision worth millions of SAR. A 403 is authenticated-but-forbidden
and is left untouched (never refreshed, never a sign-in prompt). This auth retry lives *below* TanStack
Query and is distinct from Query's transport retry. There is a direct test asserting header equality on
the original and replayed POST.

### Two decisions taken under ambiguity

1. **The transport refresher does silent-only; interactive sign-in is UI-driven.** A full-page
   `acquireTokenRedirect` fired from inside a *background* request's 401 handler would discard unsaved
   work and could race the replay. So `refreshAccessToken` performs `acquireTokenSilent({forceRefresh})`
   only, returns `null` on `InteractionRequiredAuthError`, and the route gate / header / session-ended
   state drive the interactive redirect. This still recovers the common "access token expired, refresh
   token valid" case silently (no interaction), keeps the retry loop-free and the transport layer
   unit-testable, and never turns a 403 into a prompt.
2. **The `Auth:RequireAuthenticatedReads` flag is retained, not removed.** Its removal condition (SPA
   sign-in ships) is now met for *deployed* environments, but the flag keeps the Development relaxation
   that lets a local run work without a tenant, and stays as a safety net (with the
   `RelaxedReadPostureAnnouncer` warning on any deployed host that lowers it). Retiring it entirely is
   called out as a separate backend follow-up rather than done silently here.

### Frontend

- **`lib/auth/config.ts`** — `resolveAuthConfig` (mode selection + fail-fast) and `msalConfiguration`
  (PKCE, `sessionStorage`).
- **`lib/auth/msalBridge.ts`** — installs the token provider/refresher into the transport seam and
  invalidates `identity.me` across every sign-in boundary; on sign-out it also clears the token
  provider so nothing signed-out keeps sending a bearer.
- **`lib/auth/context.ts` + `AuthProvider.tsx`** — the `AppAuth` surface (`signIn`/`signOut`/
  `switchAccount`) and `<AppAuthProvider>` (wraps `<MsalProvider>` in entra mode; a no-op in local).
- **`components/layout/AuthGate.tsx`** — the route gate: no gate in local mode; in entra mode an
  anonymous user gets a sign-in screen that preserves the deep-link `returnTo`, and the shell holds a
  `LoadingState` while identity resolves so no control flashes in and out. Whether the app renders is
  decided by the *server's* `/identity/me`, never by decoding the token.
- **`AppHeader`** — account chip (display name from `/identity/me`, never the token) + sign-out +
  switch-account when authenticated, a sign-in button when not; theme toggle and read-only badge kept.
  Rendered only in entra mode, so local mode is byte-for-byte as before.
- **`main.tsx`** — MSAL is initialised, the redirect return is processed **once** in imperative
  bootstrap (StrictMode-safe), and the token bridge is installed *before* the router renders, so the
  first `apiGet` in entra mode already carries a bearer.
- **Permission-aware rendering** — the explainability feedback control now reads
  `useHasPermission('explanation-feedback.submit')` and degrades to an explanatory line when hidden,
  matching the S6/S3 pattern.

### Edge cases handled (and tested)

Silent SSO on cold load · `consent_required`/`interaction_required` → redirect, not a dead 401 · popup
blocked → redirect primary path · third-party cookies blocked → redirect + PKCE, no iframe-silent
dependency · access token expires mid-session → silent refresh → replay with the **same idempotency
key** · session expired (`login_required`) → sign-in screen, no redirect loop · account switch →
deterministic active account · multiple tabs → next token use recovers or prompts · sign-out → provider
cleared, `identity.me` invalidated, signed-out state · StrictMode double-invoke → redirect handling is
idempotent (imperative bootstrap, not an effect) · first query before MSAL init → provider installed
pre-render · local-mode parity → no bearer, no gate, renders as today · secure-context fallback for
`newIdempotencyKey` → unchanged.

### Tests — 234 total (was 200)

34 web tests added: `client.test.ts` (10 — bearer attach/omit, 401→single silent refresh→retry,
401→refresh fails→typed `ApiError(401)` and no loop, **same `Idempotency-Key` on the original and the
replayed POST**, 403 never a sign-in prompt, AbortSignal forwarding) · `config.test.ts` (9 — mode
selection, fail-fast on missing/typo'd entra config, redirect defaulting, `sessionStorage`) ·
`msalBridge.test.ts` (5 — token install, no-account, interaction-required → no token/no loop, sign-out
clears the provider + invalidates identity, login invalidates identity) · `signin-flow.test.tsx` (9 —
account chip + sign-out/switch, sign-in when anonymous, route gate redirects anonymous and preserves
`returnTo`, `LoadingState` with no control flash, **local-mode parity**) · `ExplainabilityDrawer`
(+1 — feedback control hidden without its permission, with the explanatory line). The existing
`NavRail` shell test was updated to provide the `QueryClient` the shell now needs (it integrates
identity) — a correct update, not a relaxation.

### Backend / config

No backend code changed. The recommended staging-like verification recipe (run the API with
`Auth:Provider = EntraId`, real `Authority`/`Audience`, `RequireAuthenticatedReads = true`, and the SPA
in `entra` mode) is documented in [`src/web/README.md`](../../src/web/README.md). New `VITE_` variables
are documented in [`src/web/.env.example`](../../src/web/.env.example).

### Known gaps

- **Real-token end-to-end** of the redirect/callback/token-attach path is not exercised here (no live
  tenant; unit/component tests mock MSAL at the module boundary). S12 covers the sign-in UI wiring
  end-to-end; a real-token E2E against a test JWKS is a tracked follow-up.
- **Bundle size** — MSAL adds ~250 kB (min) to the shared index chunk. Code-splitting it so `local`
  builds never load it is a follow-up optimisation; the build passes with only Vite's existing
  chunk-size *warning*.
- **Full removal of `Auth:RequireAuthenticatedReads`** is a separate backend follow-up (see decision 2).

### Verification

typecheck ✅ · lint ✅ · web build (local mode) ✅ · build guard fails a misconfigured entra build ✅ ·
**234/234 web** ✅ · **885/885 backend** unaffected (no backend change) · architecture unaffected.

**Next action.** None — slice closed. Real-token E2E and MSAL code-splitting are tracked follow-ups.

## S12 — E2E, visual, a11y & coverage hardening · **Complete**

- **Requirements.** `V3-QA-001` (E2E), `V3-QA-002` (visual regression, 7 viewports), `V3-QA-003`
  (automated a11y scans), `V3-QA-004` (coverage tooling + threshold). All four test categories were
  absent before this slice; every prior slice logged "no visual-regression coverage yet (S12)". This
  closes that.
- **Outcome.** Four new quality gates, wired into CI. Viewports: `360 / 390 / 768 / 1024 / 1280 / 1440
  / 1920`.

### Coverage (V3-QA-004)

`@vitest/coverage-v8` with `text` + `html` + `lcov` reporters. Thresholds are set just below the
measured floor (stmts 87.4 · branch 81.9 · funcs 75.5 · lines 87.4 → gate at 86 / 80 / 74 / 86) so the
suite clears them without flaking and coverage can only ratchet up. Generated, bootstrap, config and
test files are excluded. `npm run test:coverage` runs it; the CI `web` job enforces it and uploads
`lcov.info`.

### Accessibility (V3-QA-003) — two layers

- **Component-level** (vitest + `vitest-axe`, jsdom): the design-system primitives and the populated
  explainability drawer, in **both themes**, asserting **zero serious/critical** violations. jsdom has
  no real layout, so `color-contrast` is disabled here — it is only meaningful, and is checked, at the
  route level in a real browser.
- **Route-level** (Playwright + `@axe-core/playwright`): every route in `navigation.ts`, both themes,
  plus the explainability and decision-detail drawer open states, asserting zero serious/critical.
  Contrast is real here. No blanket disables; any accepted violation would be allow-listed individually.

### End-to-end (V3-QA-001) — Playwright

`playwright.config.ts` with the seven viewports as projects, a two-server `webServer` (the API in
LocalDev — waiting on `/health/ready` so the seed is present — and the built SPA served by `vite
preview`, made same-origin with the API by a new `preview.proxy`), retries only in CI, `trace:
on-first-retry`, and web-first assertions throughout (**no `waitForTimeout`**). Journeys: shell/smoke,
cockpit-loads, the governed decision workflow (claim → accept → **self-sign-off refused server-side**,
the append-only log grows, **no delete control exists**), explainability on three screens with feedback
round-trip, CSV export (injection-safe), read-only posture / empty state, and local-mode sign-in
parity. A Playwright **global setup** posts `POST /api/v1/recommendations/records/generate` once
(idempotent) so the governed-workflow, CSV and decision-log journeys run against real rows rather than
the empty log a bare seed leaves — recommendation records are generated on demand, not seeded.

### Visual regression (V3-QA-002)

`toHaveScreenshot` per screen × 7 viewports × 2 themes. Stabilised: animations/transitions disabled
(config + injected CSS + reduced-motion context), the icon font settled (`icons-ready`) before the
shot, volatile regions masked, `maxDiffPixelRatio: 0.02`. Baselines are committed under
`e2e/__screenshots__` and platform-pinned via the `{platform}` path segment. An unreviewed pixel change
fails CI, mirroring the OpenAPI drift gate.

### E2E authentication strategy (decision)

Deterministic journeys run against the API in **LocalDev** (all roles, no tenant) with the SPA in
**`local`** mode — fully reproducible, no IdP. The sign-in journey's real-token redirect/callback path
is **not** driven against live Entra in CI (flaky, needs conditional-access exceptions). Its UI wiring
is covered at the component level (`signin-flow.test.tsx`: sign-in shown when anonymous, the gate
redirects preserving `returnTo`, account chip after sign-in, sign-out); the E2E asserts local-mode
parity. A real-token E2E against a **test JWKS** host is a tracked follow-up — the backend has no seam
to trust a test signing key today, and standing one up was out of scope for this slice (stated per
B.6).

### CI wiring

The `web` job now runs `test:coverage` (enforcing the floor) and uploads `lcov`. A new `e2e` job brings
up the seeded Postgres via `docker compose`, builds the API and SPA, installs the Chromium browser, and
runs the functional + a11y gate (`--grep-invert @visual`) then the visual gate (`--grep @visual`),
uploading the Playwright report, traces and visual diffs on failure. The existing
`backend`/`integration`/`openapi`/`ml`/`infra` jobs are untouched.

### Determinism & flake policy

No `waitForTimeout`; web-first assertions only. Screenshots: animations off, fonts settled, volatile
regions masked, baselines platform-pinned. CI retries capped at 1 (`on-first-retry` trace); a test that
only passes on retry is to be quarantined and tracked, not left green. Seed data is fixed; specs vary by
index, never by random input.

### Verification

**Run for real against Docker** (Postgres up, API in LocalDev, SPA built and previewed): the Playwright
**functional + accessibility** suite is **green — 79 passed, 0 failed** across the viewport matrix. The
governed decision workflow runs end-to-end (claim → accept → **self-sign-off refused server-side** →
the append-only log grows → **no delete control**), CSV export round-trips (injection-safe), feedback
round-trips on three screens, and every route + drawer passes the axe scan. The **visual** pipeline was
validated locally (screenshots capture cleanly with stabilisation + masking); its committed baselines
are platform-pinned and are generated on the CI Linux runner (`npm run e2e:update`), not on this
Windows box, so the Windows baselines were discarded rather than committed.

The real run **caught and fixed four issues static checks could not**: the nav rail is collapsed at
mobile widths (open it first); the Inventory explain trigger sits behind an async row-detail (switched
the journeys to direct-trigger screens); the decision log is empty on a bare seed (added the
recommendation-generating global setup); and two pre-existing serious a11y rules
(`color-contrast`, `scrollable-region-focusable`) that the suite correctly surfaced (baselined — see
below).

Also verified: `lint` ✅ · app + **e2e `tsc -p e2e/tsconfig.json`** ✅ · **244/244 vitest** ✅ including
the 10 component-a11y tests · **coverage gate** ✅ (87.0 / 81.9 / 75.5 / 87.0 over the 86 / 80 / 74 / 86
floor) · web `build` ✅.

### Known gaps / follow-ups

- **Two pre-existing a11y rules are baselined** (documented in `e2e/a11y.spec.ts`, tracked in R-11):
  `color-contrast` (the v3 palette's muted-text tokens fall below WCAG AA on some surfaces) and
  `scrollable-region-focusable` (the data-table wrapper and the drawer body lack `tabindex` — adding it
  risks the drawer focus-trap, so it is a scoped fix). Every other serious/critical rule is enforced at
  zero. Each is a separate, tracked remediation; remove the id from the allow-list as its debt is paid.
- **Visual baselines** — the pipeline is validated, but the committed Linux baselines are generated on
  the CI runner (`npm run e2e:update`) and committed there; the `@visual` step is red until they land.
  This is the expected one-time bootstrap for platform-pinned baselines, not a defect.
- **Real-token sign-in E2E** — needs a test-JWKS API host profile; tracked (see the E2E auth strategy).
- **Persona-driven permission-denied E2E** — a single LocalDev principal holds every role, so a 403
  path is covered at the component level; a narrow-persona run (`Auth__LocalDevUser__Roles__0=Analyst`)
  is a follow-up.
- **Viewport sharding** — the 7-viewport matrix could be sharded to cut CI wall-clock; not done yet.

### Acceptance

The **1085**-test regression baseline stays green (885 backend untouched; web grew 200 → 244); the 384
`engine.js` parity tests are untouched (S12 changes no formula). Coverage threshold configured and met,
ratcheting only upward. The Playwright **functional + a11y** suite ran green locally against Docker
(**79 passed, 0 failed**), covering every critical journey / route / drawer; the visual specs cover
every screen × 7 viewports × 2 themes with the pixel pipeline validated. The new CI `e2e` job runs all
of it.

**Next action.** None for authoring — slice closed. Operational bootstrap: generate and commit the
Linux visual baselines on the CI runner, then the `@visual` gate goes green.

## S7 — Data Health, Lineage & Settings · **Complete**

- **Requirements.** V3-GOV-008, V3-GOV-009, V3-GOV-010, V3-PLAT-007.
- **Outcome.** Governance transparency across three screens — `/data` (Data Health), `/lineage`
  (Model & Data Lineage) and `/settings` (Settings) — answering *which data is real vs demo, how each
  metric is derived, and what thresholds drive the platform*. Read-only throughout: **no new table, no
  migration, no write path, no delete path.** It reads existing entities and existing analytics
  constants only.

### The single-source-of-truth discipline this slice is really about

A transparency screen exists to stop two places disagreeing about the truth, so nothing here re-asserts
a value the platform already owns:

- **Real counts and coverage** come from the store — `SalesFact`/`InventoryItem` row counts and the
  `SaleMonth` range — never a literal.
- **The risk constants** on Settings are projected from `RiskSettings.Default`, `RiskWeights` and
  `Bands` by reflection/reference; a unit test asserts every surfaced value **equals** the C# constant,
  so the screen cannot silently drift from the engine it describes.
- **A metric's confirmed/demo state on Lineage is derived from its basis** (demo ⇔ the basis names a
  *synthetic fixture*), not stored a second time — and UC6/UC7's demo tags are additionally
  cross-checked against the platform's authoritative `Decision.IsDemo` flag in an integration test.

### Decisions taken under ambiguity

- **Settings ships read-only; editable Settings is deliberately deferred.** An editable global risk
  configuration recomputes every UC5 score and demands its own governance — a versioned
  `SettingsRecord` + migration, `settings.manage` + `.WithIdempotency()`, a recompute/snapshot story and
  an ADR, mirroring how ADR-0006 governs decisions. That is a separate slice. This screen therefore
  *says* the values are read-only rather than implying an editor that does nothing.
- **No `coverTarget`.** The wireframe's `coverTarget` (JS `2`) was never ported to `RiskSettings`, and
  the engine does not use it. Surfacing an invented number on the one screen whose job is provenance is
  the exact failure it exists to prevent, so it is **absent** — asserted by a reflection test on
  `RiskSettings` and a served-response test. If the team wants it, port it (with a parity test) first.
- **Procurement's Lineage tag is `demo`, following the v3 wireframe** — an explicit product decision
  taken during the slice. The platform's runtime demo flag (`Explanation.IsDemoData`) marks **only
  UC6/UC7** demo and marks UC4 *not* demo (its recommendation runs on real sales + real lead times; the
  missing supplier feed is a stated gap). The two are reconciled honestly: Lineage is a **data-level**
  provenance view, and Procurement's *basis is the synthetic supplier & PO fixture* — a genuinely
  synthetic source, matching the Procurement provider's `LineageKind.Demo` node — so tagging it demo is
  true at the data level even though the output-level `IsDemoData` is false. UC6/UC7 (the metrics the
  platform authoritatively flags) stay bound to that flag so they can never drift.
- **Demo sources on Data Health are labelled synthetic/illustrative, never a measured count.** The
  seeded store has no Procurement rows and one *combined* synthetic after-sales/parts batch, so a
  per-source measured number would be a misattribution. The four demo rows and the one blocked row are
  honest declarative rows; only the two real sources report measured counts. This also keeps the
  `DataQuality` module clear of any other module's types (rule 3).
- **"Data Management" → "Data Health".** The existing platform nav item (`/data`, `data-quality`) *is*
  the v3 "Data Health" screen; its label and page title are reconciled to v3. The nav item id is
  unchanged, so the router key and the cockpit's drill-down contract are untouched.
- **Branch base.** Written against `main`, but local `main` holds only the two Init commits — the whole
  platform (UC1–UC8, S4b, S12) lives on the `feature/spa-sign-in-test-hardening` tip. The S7 branch was
  cut from that tip (S4b + S12 landed), which is the intended baseline the prompt assumes; cutting from
  the literal empty `main` would have dropped every module and the Playwright infra S7 extends.

### Backend

- **`DataQualityCalculator`** (`BeeEye.Analytics/DataQuality/`) — a faithful port of `engine.js`
  `dataQuality()`: duplicate stock/chassis, revenue-reconciliation (>1%, in `decimal`), lead-time
  reconciliation (>2d), negatives, location mismatch, the exact score formula and its 0–100 clamp with
  `engine.js`'s round-half-up, plus a `ScoreBand` helper (≥85 Healthy · ≥70 Warning · <70 Critical).
- **Three scaffold modules promoted to `operational`** — `DataQuality` (`GET /data-quality/health`,
  `data-quality.view`), `ModelsAndExperiments` (`GET /models/lineage`, `model.view`),
  `PlatformAdministration` (`GET /platform-admin/settings`, new `settings.view`). Each adds a
  `Status => "operational"` override **and** fixes the inline `"scaffolded"` in its `ModuleInfo`.
- **New read permission `settings.view`** in `Permissions.All` (not `StateChanging` — `settings.manage`
  is state-changing and would throw if handed to `RequireReadPermission`), mapped to **Analyst + IT-Admin**,
  matching the `DataQualityView`/`ModelView` audience of the sibling governance screens. The
  permission-catalogue reflection and threat-model tests were extended.
- **`DataHealthReadService`** counts current (`"completed"`) batches only, derives coverage, and composes
  the seven sources via a **pure** static helper (unit-testable without a DB); the DB-reading path is
  covered end-to-end at the integration layer. `LineageCatalog` is declarative static data;
  `SettingsReadService` projects the constants.

### Frontend

- Three typed clients (`lib/api/{dataHealth,lineage,settings}.ts`) mirroring `executive.ts`, and three
  screens replacing the scaffolds / adding `/lineage`: score + band chip, the seven-source table with
  **word+icon+colour** status chips (demo via `<AiLabel kind="demo" />`, blocked as a first-class
  state), the DQ issues, a six-stage pipeline, an eight-metric provenance table, and the read-only
  configuration cards badged "current configuration · read-only". **Never colour alone** — every status
  and band renders its word. CSV export (V3-PLAT-007) on the Data Health sources and the Lineage
  metrics, reusing `lib/csv.ts` (formula-injection-safe). The `lineage` nav item joins the registry and
  the header comment drops Data Health & Lineage from the "not built yet" list.

### Edge cases handled and tested

Superseded batches (count only current); empty database (coherent zeroed state, score well-defined,
demo/blocked rows still list); mismatch present/absent flips Inventory Ready ↔ Ready-with-assumptions in
lock-step with the `loc` issue; score-band boundaries at exactly 85 and 70 and the 0/100 clamp;
single-month and no-sales coverage; the blocked source distinct from a demo source; no synthetic count
presented as measured; Settings drift and the `coverTarget` absence; Lineage demo/confirmed cross-check;
auth posture (401 anon deployed / 403 wrong role / 200 right role / relaxed in Dev); CSV injection;
culture-invariant formatting; and **no `DELETE` and no new table** under the three routes.

### Tests — 1258 total (was 1129)

- **Analytics +22** (384 → **406**): `DataQualityCalculatorTests` — each issue type, the exact penalty
  formula, the 0-clamp, round-half-up, and the band boundaries at ≥85/≥70.
- **Backend unit +31** (297 → **328**): the Data Health source composer (status derivation, demo/blocked
  rows, empty DB), `LineageCatalog` (6 stages in order, 8 metrics, state ⇔ synthetic-basis, the demo
  set), `SettingsReadService` (values equal `RiskSettings.Default`/`RiskWeights` by reflection, labels
  from `Bands`, **no cover-target**), and the extended permission-model tests.
- **Integration +40** (→ **242**): three API suites — `operational` status; the 7 sources and their
  statuses; real counts **291 / 3120 / 3 current batches**; score in band; coverage parses; the 6 stages
  and 8 metrics with the UC6/UC7 demo tags cross-checked against the cockpit's `isDemo`; weights
  30/25/20/15/10, bands 34/59/79, aging 30/60/90/120, analysisDate, trailingMonths 3, coverMax 6, no
  cover-target; per-route 401/403/200 added to `SecurityApiTests`; **no `DELETE`** (live + served
  document) and **the table set unchanged** (S7 adds none).
- **Web +32** (244 → **276**): three page tests (loading / empty / error+retry / populated; status and
  band by **word**; demo `AiLabel`s; the 6 stages; the 8 metrics; invariant-formatted settings;
  CSV-export formula-injection neutralised) plus the three screens added to the `vitest-axe` component
  scans (both themes, zero serious/critical).
- **Playwright**: a `governance.spec.ts` journey per screen (data-backed, labelled) and the three routes
  added to the route-level a11y and visual matrices. The three journeys and the six route-a11y scans
  (both themes) were run green in a real browser against the seeded Docker backend.

### Verification

`dotnet build BeeEye.slnx` → **0 warnings, 0 errors**; backend **982/982** (`Analytics 406 · UnitTests
328 · ArchitectureTests 6 · IntegrationTests 242`), the 384 `engine.js` parity tests untouched.
Web `lint` ✅ · `typecheck` ✅ · **276/276 vitest** ✅ · **coverage gate** ✅ (88.3 / 83.0 / 77.7 / 88.3
over the 86 / 80 / 74 / 86 floor) · `build` ✅. `tests/architecture` green (no cross-module type
references). OpenAPI snapshot regenerated (`data-quality/health`, `models/lineage`,
`platform-admin/settings` present; `schema.d.ts` is git-ignored). Playwright governance + route-a11y run
green against Docker.

### Known gaps / follow-ups

- **Visual baselines** — the three screens joined the `@visual` matrix (Data Health & Settings changed
  content; Lineage is new). Baselines are platform-pinned and CI-bootstrapped: run `npm run e2e:update`
  on the Linux runner and commit under `e2e/__screenshots__`. Not generated on this Windows box (they
  would churn). The functional E2E + a11y gates do not depend on them and are green.
- **Editable Settings** — deferred as above (its own slice: versioned record + migration +
  `settings.manage` + recompute story + ADR).
- **`coverTarget`** — not ported; if wanted, add it to `RiskSettings` with a parity test before
  surfacing it here.

**Next action.** None for authoring — slice closed. Operational bootstrap: generate and commit the
Linux `@visual` baselines for the three governance routes on the CI runner.
