# V3 Test Strategy

> How the v3 programme is verified. Extends the existing strategy rather than replacing it.
> Baseline figures: [`v3-baseline.md`](v3-baseline.md).

## 1. Starting position

| Category | Framework | Baseline | After S1+S2 |
|----------|-----------|----------|-------------|
| Backend unit | xUnit | 18 | 18 |
| Analytics unit | xUnit | 332 | **366** |
| Architecture / boundary | xUnit | 4 | 4 |
| Integration (Testcontainers Postgres) | xUnit | 33 | 33 |
| Web component / unit | Vitest + Testing Library | 17 | **45** |
| **Total** | | **404** | **466** |
| End-to-end | — | **absent** | absent |
| Visual regression | — | **absent** | absent |
| Accessibility (automated) | — | **absent** | absent |
| Contract | — | **absent** | absent |
| Performance | — | **absent** | absent |
| Security | — | **absent** | absent |
| Coverage tooling | — | **absent** | absent |

> **Current totals (post-S6, 2026-07-24):** **885 backend** (297 kernel/domain unit · 384 analytics ·
> 6 architecture · 198 integration) + **200 web** = **1085**, plus ~15 ML. The "After S1+S2" column
> above is a historical checkpoint; live per-slice counts are in [`v3-progress.md`](v3-progress.md).

The four absent categories are introduced in **S12**; the gap is recorded honestly rather than
papered over. No coverage percentage is quoted anywhere, because no coverage tool is configured —
quoting one would be fabrication.

## 2. Principles

1. **Behaviour and risk over percentages.** Coverage targets are not chased with low-value tests.
2. **Every test cites a requirement ID.** Test names and `describe` blocks reference `V3-*` IDs.
3. **Expected values are hand-computed from the source formula**, never copied from the
   implementation's own output — the existing analytics tests already do this and it is why they are
   trustworthy.
4. **Deterministic data only.** No wall-clock, no unseeded randomness, no production data. The pinned
   analysis date (`2026-06-30`) is used, matching the ADR-0006 guardrail.
5. **A slice is not done until its stated tests pass**, and the full regression suite passes with it.

## 3. Coverage by layer

### 3.1 Unit — domain, calculation, state

Must cover: the decision priority model and every decision rule (S2) · the ADR-0006 lifecycle state
machine and **every guard**, including invalid transitions (S5) · modification deltas, self-approval
rejection and the 0–20 % discount bound (S6) · data-health score bands (S7) · validation, mapping and
formatting · AI structured-output validation and rejection of number-altering responses (S10).

**Parity rule.** UC2/UC5 formulas are a faithful port of `engine.js`, which is **byte-identical
between v1 and v3** — so v3 introduces no formula change there, and the 384 analytics tests are the
regression gate. UC1/3/4/6/7 follow `docs/product/use-cases/`; UC6/UC7 synthetic fixtures are
**deliberately not** numerically compared to v3 (see V3-CONFLICT-4).

### 3.2 Integration — API, persistence, concurrency

Testcontainers Postgres, migrating from empty. Must cover: every new endpoint's contract and
`ProblemDetails` shape · authentication and authorization, including anonymous and wrong-role access
(S4) · **duplicate submission, replayed idempotency key, concurrent update, stale row version,
partial-failure rollback** (S5) · full recommendation → decision → outcome lifecycle (S6) ·
clean-database migration and upgrade from the current schema.

### 3.3 Component — rendering, interaction, accessibility

Testing Library. Every screen must test **loading, empty, error and populated** states, plus
permission-denied once S4 lands. Interaction tests use `fireEvent` (no `user-event` dependency has
been added). Accessibility assertions — roles, accessible names, `aria-current`, `aria-expanded`,
focus behaviour — are written as ordinary component tests, as S1 demonstrates.

**Gotcha recorded from S1.** Several v3 group labels are substrings of one another
("Sales Intelligence" ⊂ "After-Sales Intelligence"), so role queries must be **anchored**; an
unanchored regex silently matches two elements.

### 3.4 End-to-end — S12

Critical journeys: navigate · filter · view detail · open explainability · accept a recommendation ·
accept with modification · reject · view the Decision Log · export · permission denial · session
expiry. Animations disabled; deterministic seed data.

### 3.5 Visual regression — S12

Viewports **360×800, 390×844, 768×1024, 1024×768, 1280×800, 1440×900, 1920×1080**. Cover the shell,
each module landing page, major tables, the explainability drawer, modals, and the empty/loading/error
states. Volatile content (timestamps, generated IDs) masked; thresholds tight enough to catch real
regressions.

### 3.6 Accessibility — S12

Automated scans on every route, **supplemented by manual keyboard checks** — automation alone does not
prove a focus trap works. Target WCAG 2.2 AA.

### 3.7 Security — S12

Unauthorised endpoint access · invalid and substituted identifiers · over-posting · dangerous payloads
· rate limiting · sensitive-error leakage · open redirect. Cross-tenant tests are **not applicable** —
the platform is single-tenant by design (V3-AUTH-005).

### 3.8 Performance

Assert response-time budgets against the [baseline table](v3-baseline.md#44-api-latency-localhost-warm-postgres-single-user).
Specifically guard the two known slow endpoints (`after-sales/service-intensity/summary` at 669 ms
warm, `spare-parts/demand/summary` at 275 ms) so S8's fix is proven and does not regress.

## 4. Test data

Deterministic fixtures covering: empty dataset · single record · large result set · missing optional
fields · maximum-length and Unicode values · historical and recently-updated records · concurrently
modified records · pending, completed, rejected and superseded workflows · AI success, low-confidence,
invalid-output, timeout and provider-failure paths.

## 5. CI gates

Every slice must leave green: `dotnet build` (target: **0 warnings** after S0) · `dotnet test
BeeEye.slnx` · `npm run typecheck` · `npm run lint` · `npm run test` · `npm run build`. From S12,
e2e, visual and accessibility suites join the gate.

> `dotnet test` accepts a solution **or a single** project — passing several project paths in one
> invocation errors.
