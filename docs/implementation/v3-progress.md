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
| **S4** | **Identity, roles & authorization** | P0 | **Complete** (backend) |
| **S5** | **Recommendation records & write path** | P0 | **Complete** |
| S6 | Decision Log & human decisions | P1 | Ready (unblocked) |
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
| After S2 (complete) | 470 | 67 | 537 |
| **After S4 + S5** | **615** | **67** | **682** |

Backend breakdown after S5: `BeeEye.UnitTests` **129** (was 18) · `BeeEye.Analytics.Tests` **384**
(was 332) · `BeeEye.ArchitectureTests` 4 · `BeeEye.IntegrationTests` **98** (was 33).
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

## S6 — Decision Log & human decisions · **Ready**

- **Unblocked.** S4 supplied identity (`decided_by`) and S5 supplied the append-only substrate.
- **Conflict to resolve on entry.** V3-CONFLICT-1/2 — implement ADR-0006's model, not the prototype's
  mutable/`localStorage` one, while preserving v3's visual design and status vocabulary.
