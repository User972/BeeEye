# V3 Implementation Plan

> Vertical slices, each delivering complete behaviour from UI to persistence and each independently
> buildable, testable and reviewable. Requirement IDs are defined in
> [`v3-design-traceability.md`](v3-design-traceability.md).
>
> **Priorities** — P0 release blocker · P1 core v3 · P2 important · P3 polish.
> **Complexity** — XS · S · M · L · XL.

## Sequencing rationale

The conventional opening order (tokens → shell → components → auth → backend) was **not** applied
verbatim, because repository evidence contradicts two of its steps:

1. **Design tokens are already aligned.** `src/web/src/styles/tokens.css` and the v3 `:root` block
   share the same OKLCH values. A tokens slice would deliver nothing, so it is dropped rather than
   performed ceremonially.
2. **The Decision Log cannot come first**, despite being v3's flagship governance feature. It requires
   a write path (the app is 100% `GET`), an audit trail, and — per ADR-0006 — a *named human actor*,
   which requires identity that does not exist. Those are three foundational slices, sequenced ahead
   of it.

What remains genuinely foundational and unblocking is the **shell and navigation** (S1), because the
router is driven by the `navItems` registry, so every later screen plugs into it.

```
S0 hygiene ─┐
S1 shell ───┼─→ S2 cockpit ──→ S3 drawer ──┐
            │                              ├─→ S6 decision log ─→ S7 governance
            └─→ S4 identity ──→ S5 records ┘
                                            └─→ S8 screens ─→ S9…S12
```

---

## S0 — Dependency & warning hygiene · P1 · XS

| Field | Value |
|-------|-------|
| **Requirements** | V3-QA-005, V3-QA-006 |
| **Outcome** | The build is warning-clean, so genuine v3 regressions are visible rather than buried in 23 pre-existing warnings. |
| **Backend** | Bump `Microsoft.OpenApi` and `System.Security.Cryptography.Xml` in `Directory.Packages.props` (18 `NU1903` high-severity advisories). Fix 5 `CS8604` nullable warnings in `ConfigurationDemand.cs` and `ForecastingReadService.cs`. |
| **Data / Auth / Audit** | None. |
| **Tests** | Existing 421 backend tests must stay green; no new tests. |
| **Migration / Flag / Rollback** | None / none / revert the version bump. |
| **Acceptance** | `dotnet build BeeEye.slnx` reports 0 warnings; test totals unchanged. |

---

## S1 — Application shell & grouped navigation · P1 · M · **COMPLETE**

| Field | Value |
|-------|-------|
| **Requirements** | V3-NAV-001, V3-NAV-002, V3-NAV-003 |
| **Outcome** | The application's information architecture matches v3: six groups with phase labels, correct ordering and icons. The rail is usable on mobile and meets WCAG 2.2 AA basics. |
| **Screens** | Every screen (shell). |
| **Components** | `config/navigation.ts`, `NavRail.tsx`, `RootLayout.tsx`, `AppHeader.tsx`, `components.css` |
| **Backend / Data / Auth / Audit** | None — deliberately a pure-frontend slice with no backend dependency. |
| **Tests** | 17 registry tests + 15 shell/rail component tests. |
| **Migration / Flag / Rollback** | None / none / revert 8 files. |
| **Acceptance** | ✅ Groups, labels, phases, ordering and icons match v3. ✅ Rail slides over content at ≤900px, dismissing on scrim/Escape/navigation. ✅ Skip link, `aria-current`, named landmarks, focus-visible. ✅ 45/45 web tests, 387/387 backend tests, lint and typecheck clean. |

**Deviations recorded:** British spelling retained ("Optimisation") against v3's US spelling, per repo
convention. No nav entries added for v3's 9 unbuilt screens — they would be dead links.

---

## S2 — UC8 Executive Decision Cockpit · P1 · L · **IN PROGRESS**

| Field | Value |
|-------|-------|
| **Requirements** | V3-UC08-001…007 |
| **Outcome** | The landing screen shows the decisions that need attention this month — ranked, explained, and traceable to the module that raised them — replacing four `—` placeholder tiles. |
| **Screens** | `/` (Decision Cockpit). |
| **Backend** | `BeeEye.Analytics/Decisions/DecisionPriority.cs` (**done**) → `DecisionFeed` composing the six v3 rules from existing UC1/3/4/5/6/7 analytics → `ExecutiveInsightsReadService` → `GET /api/v1/executive-insights/decision-feed`. |
| **Data** | None — reads existing entities. No migration. |
| **Auth / Audit** | None — read-only. |
| **Tests** | Unit: priority model (**34 done**), each decision rule, aggregates. Integration: endpoint shape, empty-data behaviour, response-time budget. Component: cockpit loading/empty/error/populated states. |
| **Migration / Flag / Rollback** | None / none / revert; the page falls back to the current module list. |
| **Acceptance** | Feed ranks by the multiplicative priority score; `critical` count matches severity-high; every decision links to its module screen; all four states render; response within the §4.4 budget. |

**Note.** The feed composes analytics via the shared `BeeEye.Analytics` engine, **not** by referencing
other modules' types — preserving module isolation (CLAUDE.md rule 3).

---

## S3 — Explainability drawer & AI label system · P1 · L

| Field | Value |
|-------|-------|
| **Requirements** | V3-DS-002, V3-DS-006, V3-DS-007, V3-UC0x-002 |
| **Outcome** | Any recommendation anywhere in the platform can answer "why?" in one consistent panel. |
| **Components** | Extend `ui/Drawer.tsx` (**add focus trap + focus restoration** — absent in both v3 and the app); new `ExplainabilityDrawer` with the 11 v3 sections; `AiLabel` chip with all 8 labels. |
| **Backend** | Explainability payload contract (recommendation, impacts, confidence + reasons, ranked drivers, evidence, assumptions, lineage, model info). |
| **Tests** | Component: each section renders/omits correctly; Escape and overlay dismissal; **focus trap and restoration**; label variants. Accessibility: `aria-modal`, focus order. |
| **Acceptance** | Drawer matches v3 geometry (474px, `max-width: 94vw`); Escape priority chain honoured; focus returns to the invoking control; all 8 labels styled per `LABELS`. |

---

## S4 — Identity, roles & authorization · P0 · L

| Field | Value |
|-------|-------|
| **Requirements** | V3-AUTH-001, V3-AUTH-002, V3-AUTH-003, V3-NAV-004 |
| **Outcome** | The platform knows who is using it — the prerequisite for any accountable decision. |
| **Backend** | Authentication scheme; principal accessor; authorization policies; role model (Sales Planning / Procurement / Inventory / Parts / After-Sales Manager, plus read-only and approver). |
| **Auth** | Server-enforced on every endpoint; UI hiding is never authorization. |
| **Audit** | Sign-in / sign-out events. |
| **Tests** | Integration: unauthenticated rejected, expired session, role-gated endpoint, direct-URL access, permission revoked mid-session. |
| **Migration / Flag / Rollback** | Identity schema (additive) / flag to keep anonymous read access during rollout / disable the flag. |
| **Acceptance** | Every state-changing endpoint requires an authenticated principal; read endpoints behave per the configured policy; **an ADR records the mechanism chosen**. |

> **Requires a new ADR** — authentication mechanism, session lifecycle and role model are an
> architectural decision not settled by any existing ADR.

---

## S5 — Recommendation records & the write path · P0 · L

| Field | Value |
|-------|-------|
| **Requirements** | V3-GOV-002/003/005/006/011/012, V3-API-001…005 |
| **Outcome** | Engine recommendations become durable, frozen, audited business records instead of text recomputed on every page load. |
| **Backend** | First write endpoints in the platform; transition service enforcing ADR-0006 guards; `ProblemDetails` contract; idempotency (ADR-0007); optimistic concurrency. |
| **Data** | New entities: `Recommendation` (frozen, with provenance stamps), `RecStatusEvent` (append-only), `AuditEvent`. Additive migration — the 9 existing entities are untouched. |
| **Audit** | Every transition appends an event; nothing is overwritten. |
| **Tests** | Unit: state machine, every guard, invalid transitions. Integration: duplicate submission, replayed idempotency key, concurrent update, stale row version, partial failure rollback, clean-DB migration. |
| **Migration / Flag / Rollback** | Expand-only / write path behind a flag / disable flag; new tables remain unused. |
| **Acceptance** | A recommendation is written once and never mutated; `current_status` is a projection of the event log; **no delete path exists**; guards from ADR-0006 §3 are enforced server-side. |

**Explicitly rejected here:** `localStorage` persistence (V3-GOV-011) and hard delete (V3-GOV-012).

---

## S6 — Decision Log & human decisions · P1 · L

| Field | Value |
|-------|-------|
| **Requirements** | V3-GOV-001, V3-GOV-004, V3-GOV-007, V3-AUTH-004 |
| **Outcome** | A governed audit trail: accepting a recommendation anywhere routes into one log showing what was advised, who decided, what they changed, and why. |
| **Screens** | New `/decisions`; drawer footers across all intelligence screens. |
| **Backend** | `ManagementDecision` (+ modification delta), `ApprovalStep`, `ActionOutcome`; accept / accept-with-modification / reject endpoints. |
| **Auth** | Decisions require an authenticated, authorised principal; **segregation of duties** — the proposer may not be the sole approver. |
| **Tests** | Unit: modification delta, self-approval rejection, discount bound 0–20% (ADR-0006 §7). Integration: full lifecycle, cross-role attempts. Component: log states, filters, empty/no-match. E2E: recommend → review → accept-modified → implemented. |
| **Acceptance** | v3's visual design and status vocabulary preserved; backed by the append-only model; free status dropdown replaced by **guard-validated transitions**; delete replaced by a terminal state; original recommendation readable beside the human's decision. |

---

## S7 — Data Health, Lineage & Settings · P2 · M

| Field | Value |
|-------|-------|
| **Requirements** | V3-GOV-008, V3-GOV-009, V3-GOV-010, V3-PLAT-007 |
| **Outcome** | Governance transparency: which sources are real vs demo, how each metric is derived, and what thresholds drive it. |
| **Screens** | `/data-health`, `/lineage`, enhanced `/settings`. |
| **Backend** | Data-health source inventory + quality scoring; lineage pipeline and metric provenance; settings read (write only if S4/S5 have landed). |
| **Tests** | Unit: score bands (>85/>70), status classification. Integration: endpoints. Component: all statuses incl. `Blocked` and `Demo data`; CSV export escaping. |
| **Acceptance** | 7 sources with correct status/colour/icon; 6-stage pipeline; 8 metrics tagged confirmed/demo; risk weights and bands shown match the canonical model. |

---

## S8 — Intelligence-screen alignment & performance · P2 · L

| Field | Value |
|-------|-------|
| **Requirements** | V3-UC01..07-001, V3-PERF-001, V3-DS-003 |
| **Outcome** | The seven live screens match v3 layout and open the explainability drawer; the slowest endpoints stop recomputing per request. |
| **Backend** | Fix per-request recomputation behind `after-sales/service-intensity/summary` (669 ms warm) and `spare-parts/demand/summary` (275 ms). Self-host fonts. |
| **Tests** | Existing 366 analytics tests must stay green (**engine.js parity is non-negotiable**); response-time assertions; component tests per screen. |
| **Acceptance** | No UC2/UC5 formula change (v1↔v3 `engine.js` is byte-identical); measured latency improves against the baseline table; no CDN font requests. |

---

## S9–S12 — Later slices

| Slice | Title | Requirements | P | Cx | Notes |
|-------|-------|--------------|---|----|-------|
| S9 | Persona, accent, density | V3-NAV-005, V3-DS-004/005 | P3 | M | Presentational only; persona carries **no** authorization meaning |
| S10 | Ask Decision Intelligence | V3-PLAT-001/002 | P2 | XL | Deterministic engine is default **and** fallback; live AI may only reword, never alter numbers; structured-output validation discards violations |
| S11 | Ingestion, Reports, Methodology, Integration | V3-PLAT-003…006 | P3 | XL | Ingestion needs the S5 write path and S4 identity |
| S12 | E2E, visual, a11y, coverage hardening | V3-QA-001…004 | P1/P2 | L | Introduces the four missing test categories; viewports 360/390/768/1024/1280/1440/1920 |

---

## Definition of done (per slice)

A slice is complete only when: layout matches v3 at desktop/tablet/mobile · every control has defined
behaviour · loading, empty, error and permission states exist · validation is server-authoritative ·
authorization is server-enforced · audit events are written where required · unit, integration and
component tests pass · the **full** regression suite passes · documentation and the traceability
status column are updated in the same commit.
