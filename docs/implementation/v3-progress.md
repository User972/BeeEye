# V3 Implementation Progress

> Living record of each vertical slice. Statuses: `Not started` · `Analysing` · `Ready` ·
> `Implementing` · `In review` · `Blocked` · `Complete`.
> Last updated: 2026-07-24.

## Summary

| Slice | Title | Priority | Status |
|-------|-------|----------|--------|
| S0 | Dependency & warning hygiene | P1 | Ready |
| **S1** | **Application shell & grouped navigation** | P1 | **Complete** |
| **S2** | **UC8 Executive Decision Cockpit** | P1 | **Complete** |
| S3 | Explainability drawer & AI label system | P1 | Ready |
| **S4** | **Identity, roles & authorization** | P0 | **Complete** (backend) |
| **S5** | **Recommendation records & write path** | P0 | **Complete** |
| **S6** | **Decision Log & human decisions** | P1 | **Complete** |
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
| **After S6** | **814** | **116** | **930** |

Backend breakdown after S5: `BeeEye.UnitTests` **129** (was 18) · `BeeEye.Analytics.Tests` **384**
(was 332) · `BeeEye.ArchitectureTests` 4 · `BeeEye.IntegrationTests` **98** (was 33).
Web: 67 (was 17). All green, 0 failures, 0 skipped.

Backend breakdown after S6: `BeeEye.UnitTests` **259** (was 129) · `BeeEye.Analytics.Tests` 384
(unchanged — S6 touches no formula) · `BeeEye.ArchitectureTests` **5** (was 4) ·
`BeeEye.IntegrationTests` **166** (was 98). Web: **116** (was 67).
All green, 0 failures, 0 skipped.

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
