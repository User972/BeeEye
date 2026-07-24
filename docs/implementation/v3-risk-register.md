# V3 Risk Register

> Risks material to delivering the v3 experience. Probability and Impact are `Low` / `Medium` / `High`.
> Status: `Open` · `Mitigating` · `Closed` · `Accepted`.
> Cross-references: [`v3-design-traceability.md`](v3-design-traceability.md),
> [`v3-baseline.md`](v3-baseline.md), [`v3-gap-analysis.md`](v3-gap-analysis.md).

> **Update (post-S6, 2026-07-24):** R-01, R-02 and R-03 are now **Closed** — the governed Decision Log
> (S6), identity & authorization (S4) and the frozen append-only write path (S5) all shipped as designed
> and under test. Any test-count figures below predate S3–S6; current totals are **885 backend + 234
> web = 1119** (S4b added 34 web tests; see [`v3-progress.md`](v3-progress.md)).

## Top risks

| ID | Risk | Prob. | Impact | Affects | Status |
|----|------|-------|--------|---------|--------|
| R-01 | Decision Log implemented as v3 draws it, discarding the audit trail | Medium | **High** | V3-GOV-001/011/012 | **Closed** (S6) |
| R-02 | Human decisions land before identity exists, producing unattributable records | Medium | **High** | V3-AUTH-001, V3-GOV-004 | **Closed** (S4→S6; SPA sign-in S4b) |
| R-03 | First write path introduces the platform's first mutation defects | High | **High** | V3-API-001…005 | **Closed** (S5, under test) |
| R-04 | Effort wasted chasing numeric parity with v3's synthetic fixtures | Medium | Medium | V3-CONFLICT-4 | **Mitigating** |
| R-05 | UC2/UC5 `engine.js` parity broken while restyling | Low | **High** | V3-UC02/05-001 | **Mitigating** |
| R-06 | Cockpit inherits the 669 ms after-sales endpoint across six modules | High | Medium | V3-PERF-001, V3-UC08-003 | Open |

---

## Detail

### R-01 — The Decision Log is built the way v3 draws it

- **Description.** v3's Decision Log is a mutable record with a free status dropdown, a hard delete and
  `localStorage` persistence. Implementing it literally destroys the audit trail, the IP/liability
  boundary between algorithm and human, and the learning loop.
- **Trigger.** A slice treats the prototype as the specification without reading ADR-0006.
- **Probability / Impact.** Medium / High.
- **Affected requirements.** V3-GOV-001, V3-GOV-011, V3-GOV-012, V3-CONFLICT-1, V3-CONFLICT-2.
- **Mitigation.** ADR-0006 (**Accepted**) already settles this and its `Supersedes` field explicitly
  names the prototype's `localStorage` behaviour. The conflict is recorded as V3-CONFLICT-1/2 with a
  mapping from v3's 9 labels onto the ADR's state machine; V3-GOV-011/012 are marked **Rejected** in
  the traceability matrix so they cannot be picked up as work.
- **Detection.** Architecture test asserting no mutable `status` setter on decision entities; code
  review against ADR-0006; integration test proving a transition appends an event rather than
  overwriting.
- **Contingency.** If a mutable shape ships, rebuild the log from `AuditEvent` history — only possible
  if V3-API-004 lands first, which is why it is P0.
- **Owner.** Platform Architecture. **Status: Mitigating.**

### R-02 — Decisions recorded without an accountable human

- **Description.** ADR-0006's central claim is that "a *named human* made the binding call". The
  application has **no authentication whatsoever** (verified: 44/44 endpoints are anonymous `GET`).
  A decision write path built before identity would record an unattributable actor.
- **Trigger.** Sequencing the Decision Log UI ahead of identity because it is more visible.
- **Probability / Impact.** Medium / High.
- **Mitigation.** Slice order enforces it: **S4 (identity) precedes S6 (human decisions)**. S5 —
  system-generated recommendation records — is deliberately placed between them because its actor is
  the *system*, so it needs no identity and can proceed in parallel.
- **Detection.** No endpoint may write a `ManagementDecision` without an authenticated principal;
  integration test asserting an anonymous request is rejected.
- **Contingency.** Hold S6 until S4 completes. Do **not** substitute a placeholder actor — that is
  precisely the fake-logic failure mode the delivery rules forbid.
- **Owner.** Security Architecture. **Status: Closed** (S4 identity → S6 human decisions, with the SPA
  sign-in flow completing the client side in S4b — the browser can now obtain, attach and refresh an
  Entra token, so a deployed host can require authentication and no decision is recorded without a
  named, accountable human).

### R-03 — First-ever write path introduces mutation defects

- **Description.** The platform has never performed a write. Idempotency, concurrency, transaction
  boundaries, audit and validation all arrive at once, with no existing pattern to copy.
- **Trigger.** S5.
- **Probability / Impact.** High / High.
- **Mitigation.** ADR-0007 (idempotency and replay) already exists and must be implemented, not
  reinvented. Build the write path once as a shared pattern (V3-API-001…005) and reuse it. Optimistic
  concurrency via row version (V3-API-003) from the first mutation, not retrofitted.
- **Detection.** Integration tests for: duplicate submission, concurrent update conflict, stale row
  version, partial failure rollback, replayed idempotency key.
- **Contingency.** Feature-flag the write path so it can be disabled without redeploying.
- **Owner.** Backend. **Status: Open.**

### R-04 — Wasted effort chasing synthetic-fixture parity

- **Description.** v3's `engine2.js` uses mulberry32 + FNV-1a-32 (`SEED = 20260531`); the repository
  uses SplitMix64 + FNV-1a-64. The two produce entirely different synthetic UC6/UC7 datasets. An
  attempt to make the app's numbers match the prototype's would mean re-porting a **demo fixture** and
  destabilising a passing implementation.
- **Probability / Impact.** Medium / Medium.
- **Mitigation.** Recorded as V3-CONFLICT-4 with an explicit decision: **do not chase parity on
  synthetic data.** CLAUDE.md already states UC6/UC7 follow `docs/product/use-cases/`, not the
  prototype. Parity remains mandatory only where `engine.js` governs (UC2/UC5).
- **Detection.** Any test asserting equality with a v3 synthetic figure is a red flag in review.
- **Owner.** Analytics. **Status: Mitigating.**

### R-05 — UC2/UC5 engine.js parity broken during restyling

- **Description.** 332 analytics tests encode the `engine.js` port. Cosmetic work on UC2/UC5 screens
  could tempt changes to the calculation layer.
- **Probability / Impact.** Low / High.
- **Mitigation.** `engine.js` is **byte-identical between v1 and v3** (md5-verified), so v3 introduces
  **zero** UC2/UC5 formula change. Any diff in those files during a v3 slice is by definition
  out of scope. CLAUDE.md rule 5 already mandates parity.
- **Detection.** The 366 analytics tests must stay green; review any diff under
  `src/shared/BeeEye.Analytics/{Forecasting,Inventory,Demand}`.
- **Owner.** Analytics. **Status: Mitigating.**

### R-06 — Decision Cockpit inherits a slow endpoint six times over

- **Description.** Baseline measurement: `after-sales/service-intensity/summary` takes **669 ms warm**
  for a 479-byte payload and does not improve on repeat, indicating per-request recomputation over the
  full history. `spare-parts/demand/summary` is 275 ms. The cockpit composes six modules, so it would
  aggregate these costs.
- **Probability / Impact.** High / Medium.
- **Mitigation.** V3-PERF-001 addresses the underlying recomputation. The cockpit's decision feed
  should compose already-computed module results rather than re-deriving them, and the feed itself is
  a candidate for a request-scoped cache (v3 memoises it in `_decCache`).
- **Detection.** Assert a cockpit response-time budget in integration tests; compare against the
  §4.4 baseline table.
- **Contingency.** Compute the feed asynchronously and serve the last completed snapshot.
- **Owner.** Performance. **Status: Open.**

---

## Standing risks

| ID | Risk | Prob. | Impact | Mitigation | Status |
|----|------|-------|--------|------------|--------|
| R-07 | Design ambiguity — v3 controls with no defined behaviour | Medium | Medium | Every ambiguity carries a conservative safe default (V3-CONFLICT-1…8); none blocks progress | Mitigating |
| R-08 | Scope expansion — v3 adds 8 new screens plus cross-cutting patterns | High | Medium | Vertical slices S0–S12, each independently shippable and reviewable; XL work (AI, ingestion) deferred to S10/S11 | Mitigating |
| R-09 | Visual regression — no visual tooling exists | High | Medium | S12 introduces Playwright `toHaveScreenshot` at 7 viewports × 2 themes with animations disabled, fonts settled, volatile regions masked and platform-pinned baselines behind a CI gate; component tests still assert structure | Mitigating |
| R-10 | Functional regression | Medium | High | 432-test green baseline; full regression run after every slice (done for S1) | Mitigating |
| R-11 | Accessibility regression | Medium | Medium | S1 added skip link, landmarks, `aria-current`, focus-visible, reduced motion; S12 adds automated scans (vitest-axe on components + @axe-core/playwright on every route, both themes) enforcing zero serious/critical except two baselined pre-existing rules (`color-contrast` palette debt, `scrollable-region-focusable`), each tracked for separate remediation; V3-DS-007 fixes the drawer focus trap | Mitigating |
| R-12 | Known high-severity dependency advisories inherited into v3 work | High | Medium | 18 `NU1903` advisories recorded in the baseline as pre-existing; V3-QA-005 remediates in S0 | Open |
| R-13 | CSP weakened by CDN fonts | Medium | Medium | V3-CONFLICT-3 — self-host (OFL-1.1 permits it) rather than allow-listing a third-party origin | Open |
| R-14 | AI cost / provider instability once V3-PLAT-001/002 land | Medium | Medium | ADR-0004 provider abstraction; deterministic engine is the **default** and the fallback, so AI failure degrades to a working experience | Open |
| R-15 | AI alters figures when rewording | Medium | **High** | Contract from v3's METHODOLOGY: live AI may only reword a deterministic draft; structured-output validation discards any response that changes a number (ADR-0006 §7) | Open |
| R-16 | Schema incompatibility / migration failure | Low | High | Expand-and-contract; new tables only (no destructive change to the 9 existing entities); Testcontainers already proves clean-DB migration | Mitigating |
| R-17 | Deployment sequencing — backend and frontend not atomic | Medium | Medium | Additive API evolution; feature flags on new write paths | Open |
| R-18 | Incomplete test coverage — no e2e/visual/a11y/perf/security suites exist | High | Medium | S12 delivers the e2e, visual, a11y and coverage suites + CI gates (perf/security remain separate follow-ups); no slice may claim "complete" without its stated tests | Mitigating |
| R-19 | Mobile layout issues — v3 is desktop-only and specifies no mobile pattern | Medium | Medium | V3-CONFLICT-7; S1 already fixed the broken ≤900px rail; each screen slice must state responsive behaviour | Mitigating |
| R-20 | Documentation drifts from implementation | Medium | Medium | Traceability status columns are updated in the same commit as the code; this register and `v3-progress.md` are living documents | Mitigating |
| R-21 | Cross-tenant data leakage | — | — | **Not applicable.** The platform is single-tenant by design; neither v3 nor the product spec introduces tenancy (V3-AUTH-005 deferred with justification) | Accepted |
