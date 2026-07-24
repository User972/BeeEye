# V3 Design Traceability Matrix

> Every v3 requirement has a stable ID. Every implementation task and every test must cite one.
> Sources: `docs/wireframes-v3/` (design), `docs/implementation/v3-design-inventory.md` (inventory),
> `docs/implementation/v3-baseline.md` (pre-change state).
>
> **Status legend** — `Implemented` · `In progress` · `Planned` · `Deferred` · `Rejected` · `Blocked`.
> **Test legend** — `Covered` · `Partial` · `None` · `N/A`.

## Status roll-up

| Status | Count |
|--------|-------|
| Implemented | 37 |
| Planned | 11 |
| Deferred | 4 |
| Rejected | 2 |
| Blocked | 1 |
| **Total** | **55** |

---

## A. Shell & navigation (`V3-NAV-*`)

| ID | V3 source | Requirement | Existing route/component | Change category | Priority | Cx | Slice | Status | Tests |
|----|-----------|-------------|--------------------------|-----------------|----------|----|----|--------|-------|
| V3-NAV-001 | `navGroupsDef()` L2384 | Six navigation groups in v3 order, with v3 labels, icons, item ordering and delivery-phase labels | `config/navigation.ts`, `NavRail.tsx` | Navigation change | P1 | M | S1 | **Implemented** | Covered (17 registry + 7 rail tests) |
| V3-NAV-002 | shell chrome | Shell accessibility: skip link, named landmarks, `aria-current`, group headings, focus-visible, reduced motion | `RootLayout.tsx`, `components.css` | Accessibility impact | P1 | S | S1 | **Implemented** | Covered (3 tests) |
| V3-NAV-003 | *(absent in v3)* | Mobile navigation: rail slides over content, scrim, Escape/route-change dismissal. **v3 defines no mobile pattern** — fixes a pre-existing defect where the rail filled the viewport at ≤900px | `RootLayout.tsx`, `AppHeader.tsx` | New interaction behaviour | P1 | S | S1 | **Implemented** | Covered (5 tests) |
| V3-NAV-004 | `navBadges()` L2393 | Live nav badges: cockpit critical count, decisions awaiting-review count, data-health `!` marker | — | New data requirement | P2 | M | S4 | Planned | None |
| V3-NAV-005 | `rvCommon()` persona tabs | Persona switcher (Executive / Business Analyst / IT-Data Steward) changing emphasis; `personaShowCharts = !IT` | — | New interaction behaviour | P3 | M | S9 | Planned | None |
| V3-NAV-006 | prototype has no router | Deep-linkable routes for every screen, preserving existing paths | `router.tsx` (already URL-based) | **App exceeds v3** | — | — | — | **Implemented** (pre-existing) | Covered |

## B. Design system (`V3-DS-*`)

| ID | V3 source | Requirement | Existing | Change category | Priority | Cx | Slice | Status | Tests |
|----|-----------|-------------|----------|-----------------|----------|----|----|--------|-------|
| V3-DS-001 | `:root` token block | OKLCH token set, light + dark | `styles/tokens.css` | Unchanged | — | — | — | **Implemented** (verified identical) | N/A |
| V3-DS-002 | `LABELS` engine2 L28 | Eight AI-output label chips (Observed · Calculated · Forecast · Recommendation · Simulation · Demo Data · Low Confidence · **Data Quality**) with exact colour/icon | `ui/AiLabel.tsx` + `ui/aiLabels.ts`; colours in `components.css` | New component | P1 | S | S3 | **Implemented** — all eight; the three hand-rolled `badge--demo` spans migrated onto it | Covered (31 component) |
| V3-DS-003 | fonts via Google CDN | Self-host IBM Plex Sans/Mono + Material Symbols (OFL-1.1 permits redistribution) rather than CDN | CDN today | Security impact (CSP) | P2 | S | S8 | Planned | None |
| V3-DS-004 | `accent` prop | Accent variants (blue/teal/indigo) | — | Cosmetic | P3 | S | S9 | Deferred | None |
| V3-DS-005 | `density` prop | Density variants (comfortable/compact) | — | Cosmetic | P3 | S | S9 | Deferred | None |
| V3-DS-006 | drawer L1710 | Shared explainability drawer: 474px, 11 sections, footer workflow actions | `domain/ExplainabilityDrawer.tsx` on the shared `ui/Drawer`; `GET /api/v1/predictions/explain` behind `IExplainabilityProvider` | New component | P1 | L | S3 | **Implemented** | Covered (44 component + 32 integration + 38 unit) |
| V3-DS-007 | — | Focus trap + focus restoration in the shared `Drawer` primitive (**absent in both v3 and the app**) | `ui/Drawer.tsx` | Accessibility impact | P1 | S | S3 | **Implemented** (brought forward into S6; S3 added the drawer **stack** so Escape and Tab reach only the topmost, and fixed an effect-churn defect that reset focus on every parent render) | Covered (6 component tests) |
| V3-DS-008 | banners | Demo-data disclosure banner | `domain/SyntheticBanner.tsx` | Unchanged | — | — | — | **Implemented** (pre-existing) | Covered (1 test) |

## C. Executive Decision Cockpit — UC8 (`V3-UC08-*`)

| ID | V3 source | Requirement | Existing | Change category | Priority | Cx | Slice | Status | Tests |
|----|-----------|-------------|----------|-----------------|----------|----|----|--------|-------|
| V3-UC08-001 | `priorityScore()` engine2 L511 | Multiplicative priority model: `round(clamp(impactF × urgency × confidence × controllability,0,1) × 100)`, `impactF = clamp(|impact|/5M, 0.15, 1)` | — | New API requirement | P1 | S | S2 | **Implemented** (`Analytics/Decisions/DecisionPriority.cs`) | Covered (34 tests) |
| V3-UC08-002 | `mkDecision()` engine2 L559 | Severity→due-days (5/12/20), confidence bands (>0.75/>0.5), four ranked factors | — | New API requirement | P1 | XS | S2 | **Implemented** | Covered (within the 34) |
| V3-UC08-003 | `decisionFeed()` engine2 L515 | Five of six cross-module decision rules (D-ORD-1, D-PRC-1, D-INV-1, D-PRT-1, D-SVC-1) published by their owning contexts via `IDecisionSignalProvider`. **D-SUP-1 excluded** — see V3-UC08-008 | `ExecutiveInsights` (was scaffold) | New workflow | P1 | L | S2 | **Implemented** | Covered (13 integration) |
| V3-UC08-004 | `rvCockpit()` L3070 | Cockpit endpoint `GET /api/v1/executive-insights/decision-feed` returning ranked decisions, aggregates and any provider gaps | 44 GET endpoints, none for this | New API requirement | P1 | M | S2 | **Implemented** | Covered (13 integration) |
| V3-UC08-005 | `rvCockpit()` financial block | Aggregates: opportunity/risk value, `critical`, `lowConfidence`, `dueThisWeek`, `demoDataCount` | — | New data requirement | P1 | M | S2 | **Implemented** | Covered (18 unit + integration) |
| V3-UC08-006 | cockpit UI | Cockpit screen replacing the four `—` placeholder stat cards, with loading/empty/error/partial-failure states | `pages/executive-cockpit.tsx` | Component change | P1 | M | S2 | **Implemented** | Covered (22 component) |
| V3-UC08-007 | cockpit narrative | Generated narrative sentence grouping decisions by area | — | Content change | P2 | S | S2 | **Implemented** | Covered (5 unit) |
| V3-UC08-008 | `decisionFeed()` D-SUP-1 | Supplier delay exposure (on-time %, average delay, PO history) | **No supplier or purchase-order entity exists** | New data requirement | P2 | L | S5+ | **Blocked** — see V3-CONFLICT-9 | N/A |
| V3-UC08-009 | *(new seam)* | `IDecisionSignalProvider` published contract letting each context contribute decisions without any module referencing another | — | New API requirement | P1 | M | S2 | **Implemented** | Covered (architecture tests 4/4) |

## D. Governance (`V3-GOV-*`) — all new in v3

| ID | V3 source | Requirement | Existing | Change category | Priority | Cx | Slice | Status | Tests |
|----|-----------|-------------|----------|-----------------|----------|----|----|--------|-------|
| V3-GOV-001 | `rvDecisions()` L3100 | Decision Log screen: rows, status chips, filters, empty/no-match states, CSV export | `pages/decision-log.tsx`, `DecisionsAndOutcomes` (operational, 9 routes) | New workflow | P1 | L | S6 | **Implemented** | Covered (29 component + 18 CSV unit + 68 integration) |
| V3-GOV-002 | ADR-0006 §4 | Persist `Recommendation` **frozen/append-only** with provenance stamps | `recommendations` table + migration | Database schema impact | P0 | L | S5 | **Implemented** | Covered (23 integration) |
| V3-GOV-003 | ADR-0006 §3 | `RecommendationStatusEvent` append-only log; `CurrentStatus` as a projection | `recommendation_status_events` table | Database schema impact | P0 | M | S5 | **Implemented** | Covered (23 integration) |
| V3-GOV-004 | ADR-0006 §4 | `ManagementDecision` + modification delta; `ApprovalStep`; `ActionOutcome` | `management_decisions`, `approval_steps`, `action_outcomes` tables + migration | Database schema impact | P0 | L | S6 | **Implemented** | Covered (68 integration) |
| V3-GOV-005 | ADR-0006 §3 guards | Guarded transitions: expiry suspended under review; supersession blocked with approval in flight; rejection needs a reason | `RecommendationLifecycle` | New validation behaviour | P0 | M | S5 | **Implemented** | Covered (29 unit) |
| V3-GOV-006 | v3 status dropdown | **Reconcile** v3's 9-status vocabulary with ADR-0006's state machine (see V3-CONFLICT-1) | `lib/api/decisions.ts` `statusLabels`/`statusColours` | Unclear design intent | P0 | M | S6 | **Implemented** — v3 labels map onto ADR-0006 states; `Assigned` and `Snoozed` **dropped** (no counterpart; ownership is `OwnerRole` + the claiming actor, and the lifecycle has no snooze) | Covered (component) |
| V3-GOV-007 | drawer footers | "Accept & log" / "Assign owner" / "Watchlist" routing a recommendation into the Decision Log | `components/domain/DecisionFooter.tsx` on UC5/UC6/UC7 drawers | New workflow | P1 | M | S6 | **Implemented** — "Accept & log" claims the persisted record; where none exists the footer says so rather than acting. "Assign owner"/"Watchlist" **not built**: they map to the dropped `Assigned`/`Snoozed` states | Partial (footer states covered via the shared hooks; no dedicated component test) |
| V3-GOV-008 | `dataHealth()` engine2 L560 | Data Health screen: 7 sources × (system, status, rows, coverage, note), DQ issues, score bands (>85/>70) | `DataQuality` (scaffold) | New workflow | P2 | M | S7 | Planned | None |
| V3-GOV-009 | `lineage()` engine2 L583 | Lineage screen: 6-stage pipeline + 8 metrics tagged confirmed/demo | `ModelsAndExperiments` (scaffold) | New workflow | P2 | M | S7 | Planned | None |
| V3-GOV-010 | settings defaults | Settings screen surfacing risk weights (30/25/20/15/10), bands (34/59/79), aging bands, horizon, CI | `pages/platform-settings.tsx` | Component change | P2 | M | S7 | Planned | None |
| V3-GOV-011 | `localStorage` L1979 | **Reject** browser-local decision persistence in favour of ADR-0006 server records | — | Security / data integrity | P0 | — | S5 | **Rejected** (ADR-0006 supersedes) | N/A |
| V3-GOV-012 | `deleteAction()` L1986 | **Reject** hard delete of decision records; use a terminal state instead | — | Data integrity | P0 | — | S5 | **Rejected** (ADR-0006 append-only) | N/A |

## E. Identity & authorization (`V3-AUTH-*`) — prerequisite, absent from v3

| ID | Driver | Requirement | Existing | Change category | Priority | Cx | Slice | Status | Tests |
|----|--------|-------------|----------|-----------------|----------|----|----|--------|-------|
| V3-AUTH-001 | ADR-0006 `decided_by` | Authenticated user identity, so a decision can name the human accountable | Entra ID OIDC/PKCE + guarded dev provider | New permission requirement | P0 | L | S4 | **Implemented** (backend; SPA sign-in outstanding) | Covered (18 integration) |
| V3-AUTH-002 | ADR-0006 liability | Server-enforced authorization on every state-changing endpoint | Permission policies, unconditional for state-changing | Security impact | P0 | M | S4 | **Implemented** | Covered (18 integration) |
| V3-AUTH-003 | threat model §3.2 | Role model: Executive / Analyst / IT-Admin mapped to 25 permissions | — | New permission requirement | P1 | M | S4 | **Implemented** | Covered (64 unit) |
| V3-AUTH-004 | ADR-0006 §6 | Segregation of duties: no role holds both sides of an author/approve pair, **and no person approves their own decision** | `RolePermissions.AuthorApprovePairs`, `SubjectIds.Same`, `DecisionService.SignOffAsync` | New validation behaviour | P1 | S | S4 + S6 | **Implemented** (all three layers: permission, actor, and a documented exemption for outcome recording) | Covered (unit + integration) |
| V3-AUTH-005 | — | Multi-tenancy | None; single-tenant by design | — | — | — | — | **Deferred** — not a v3 requirement; no tenant concept in v3 or the product spec | N/A |

## F. Write-path infrastructure (`V3-API-*`)

| ID | Driver | Requirement | Existing | Change category | Priority | Cx | Slice | Status | Tests |
|----|--------|-------------|----------|-----------------|----------|----|----|--------|-------|
| V3-API-001 | first mutations | First write endpoint: `POST /api/v1/recommendations/records/generate` | — | New API requirement | P0 | M | S5 | **Implemented** | Covered (23 integration) |
| V3-API-002 | ADR-0007 | Deterministic idempotency key + unique index; concurrent runs collapse to a no-op; **`Idempotency-Key` header honoured on every human write** (ADR-0007 §2.1 / invariant I-6) | `Shared.Web/Idempotency/*`, `idempotency_records` | New API requirement | P0 | M | S5 + S6 | **Implemented** | Covered (4 + 7 integration, 24 unit) |
| V3-API-003 | concurrency | Optimistic concurrency via PostgreSQL `xmin` row version on `Recommendation` | — | Data impact | P0 | S | S5 | **Implemented** | Covered (schema) |
| V3-API-004 | audit | `AuditEvent` append-only trail with before/after hashes | `Audit` (scaffold) | New data requirement | P0 | M | later | **Deferred** — out of scope for S6; the status-event log carries the trail. Tracked as tech-debt TD-4 | None |
| V3-API-005 | error contract | Consistent `ProblemDetails` on all new endpoints | `DecisionEndpoints.Problem` — one mapping for every domain failure | Changed API contract | P1 | S | S6 | **Implemented** | Covered (integration asserts each status and its detail) |

## G. Platform & AI screens (`V3-PLAT-*`)

| ID | V3 source | Requirement | Existing | Change category | Priority | Cx | Slice | Status | Tests |
|----|-----------|-------------|----------|-----------------|----------|----|----|--------|-------|
| V3-PLAT-001 | `rvAI()` | Ask Decision Intelligence: grounded Q&A, deterministic insight engine, suggested questions | None | New AI capability | P2 | XL | S10 | Planned | None |
| V3-PLAT-002 | `.env.example`, METHODOLOGY | Optional live-AI refinement that may **only reword**, never alter numbers; deterministic fallback on any failure | ADR-0004 (abstraction) | New AI capability | P2 | L | S10 | Planned | None |
| V3-PLAT-003 | `rvIngest()` | Data Ingestion: upload + AI column mapping + validation states | None | New workflow | P3 | XL | S11 | Planned | None |
| V3-PLAT-004 | `rvReports()` | Reports & Exports screen | None | New workflow | P3 | M | S11 | Planned | None |
| V3-PLAT-005 | `rvMethod()` | Methodology screen | None | New screen | P3 | S | S11 | Planned | None |
| V3-PLAT-006 | `rvIntegration()` | Integration Blueprint screen | None | New screen | P3 | S | S11 | Planned | None |
| V3-PLAT-007 | `exportCSV()` | CSV export with RFC-4180 escaping across screens | `lib/csv.ts` (used by the Decision Log; other screens adopt it in S7) | New interaction | P2 | S | S6 + S7 | **Implemented** (helper + Decision Log); other screens Planned | Covered (18 unit incl. formula injection) |
| V3-PLAT-008 | `rvActions()` | Management Actions — **superseded** by the Decision Log (absent from v3's `startScreen` enum) | None | Deprecation | P3 | — | — | **Deferred** — retain nothing; never built, and v3 supersedes it | N/A |

## H. Existing intelligence screens (`V3-UC01..07-*`)

| ID | Use case | Requirement | Existing | Change category | Priority | Cx | Slice | Status | Tests |
|----|----------|-------------|----------|-----------------|----------|----|----|--------|-------|
| V3-UC01-001 | UC1 | Order Optimisation aligned to v3 layout + explainability drawer entry | `pages/order-optimisation.tsx` (live) | Layout change | P2 | M | S8 | Planned | Partial |
| V3-UC02-001 | UC2 | Forecast Accuracy aligned to v3 | `pages/sales-forecasting.tsx` (live) | Layout change | P2 | M | S8 | Planned | Partial |
| V3-UC03-001 | UC3 | Configuration Insights aligned to v3 | `pages/configuration-demand.tsx` (live) | Layout change | P2 | M | S8 | Planned | Partial |
| V3-UC04-001 | UC4 | Procurement Optimisation aligned to v3 + supplier demo banner | `pages/procurement.tsx` (live) | Layout change | P2 | M | S8 | Planned | Partial |
| V3-UC05-001 | UC5 | Inventory Aging & Overstock aligned to v3 | `pages/inventory-intelligence.tsx` (live) | Layout change | P2 | M | S8 | Planned | Partial |
| V3-UC06-001 | UC6 | Sales ↔ Service Correlation aligned to v3 | `pages/after-sales.tsx` (live) | Layout change | P2 | M | S8 | Planned | Partial |
| V3-UC07-001 | UC7 | Spare Parts Prediction aligned to v3 | `pages/spare-parts.tsx` (live) | Layout change | P2 | M | S8 | Planned | Partial |
| V3-UC0x-002 | all | Explainability drawer wired into each intelligence screen | All seven intelligence screens + the cockpit + the Decision Log | New interaction | P1 | L | S3 | **Implemented** (9 of 9 screens) | Covered (7 wiring + 1 cockpit + 1 Decision Log) |
| V3-PERF-001 | UC6/UC7 | Fix per-request recomputation: `after-sales/service-intensity/summary` is 669 ms **warm** for a 479-byte payload | `AfterSalesReadService` | Performance impact | P1 | M | S8 | Planned | None |

## I. Quality gates (`V3-QA-*`)

| ID | Requirement | Existing | Priority | Cx | Slice | Status |
|----|-------------|----------|----------|----|----|--------|
| V3-QA-001 | End-to-end tests for critical journeys | **None exist** | P1 | L | S12 | Planned |
| V3-QA-002 | Visual regression at 7 viewports | **None exist** | P2 | L | S12 | Planned |
| V3-QA-003 | Automated accessibility scans on all routes | **None exist** | P1 | M | S12 | Planned |
| V3-QA-004 | Coverage tooling + threshold | **None configured** | P2 | S | S12 | Planned |
| V3-QA-005 | Remediate `NU1903` high-severity advisories (`Microsoft.OpenApi` 2.0.0, `System.Security.Cryptography.Xml` 9.0.0) | 18 warnings | P1 | S | S0 | **Implemented** (transitive pins: `Microsoft.OpenApi` 2.11.0, `System.Security.Cryptography.Xml` 10.0.10) |
| V3-QA-006 | Clear the 5 pre-existing `CS8604` nullable warnings | 5 warnings | P2 | XS | S0 | **Implemented** (build is 0-warning) |

---

## Conflicts requiring a decision

| ID | Screen | Conflict | Recommended safe default | Risk if ignored |
|----|--------|----------|--------------------------|-----------------|
| **V3-CONFLICT-1** ✅ *resolved in S6* | Decision Log | v3 uses a **mutable status dropdown + hard delete + `localStorage`**; ADR-0006 (Accepted) explicitly **rejects** exactly this ("single mutable record" is its rejected Option B) and its `Supersedes` field names the prototype's `localStorage` behaviour | Keep v3's visual design and status vocabulary; back it with ADR-0006's append-only record + status-event log. Replace the free dropdown with **guard-validated transitions** and delete with a **terminal state**. | Loss of audit trail, IP/liability boundary and learning loop — the ADR's stated reasons |
| **V3-CONFLICT-2** ✅ *resolved in S6* | Decision Log | v3's 9 statuses (`New`…`Superseded`) ≠ ADR-0006's 9 states (`Generated`…`OutcomeRecorded`). v3 adds `Assigned`, `In progress`, `Snoozed`; ADR adds `AcceptedModified`, `Expired`, `Implemented`, `OutcomeRecorded` | Adopt the ADR state machine as canonical; map v3 labels onto it (`New`→Generated, `Completed`→Implemented). Treat `Assigned`/`Snoozed` as **assignment//deferral attributes**, not lifecycle states, so the machine stays sound | Ambiguous lifecycle, unenforceable guards |
| **V3-CONFLICT-3** | all | v3 loads fonts from the **Google Fonts CDN** | Self-host (IBM Plex + Material Symbols are OFL-1.1, redistribution permitted) | CSP weakening, third-party runtime dependency, offline failure |
| **V3-CONFLICT-4** | UC6/UC7 | v3's synthetic fixtures use **mulberry32 + FNV-1a-32** (`SEED = 20260531`); the repo uses **SplitMix64 + FNV-1a-64** — numerically irreconcilable | **Do not chase numeric parity on demo fixtures.** CLAUDE.md already specifies UC6/UC7 follow `docs/product/use-cases/`. Parity remains mandatory for UC2/UC5 (`engine.js`, byte-identical v1↔v3) | Wasted effort re-porting a demo fixture; risk of destabilising a passing implementation |
| **V3-CONFLICT-5** ✅ *resolved in S4/S6* | all | v3 has **no auth**, yet its Decision Log records an `owner` and ADR-0006 requires a named `decided_by` | Sequence identity (S4) **before** human decisions (S6). The system-generated recommendation layer (S5) needs no identity and can land first | Unattributable decisions — fails the ADR's core liability requirement |
| **V3-CONFLICT-6** ✅ *resolved in S6* | Decision Log | v3 internal inconsistency: `addAction()` defaults `status: "Proposed"`, absent from its own 9-status list | Use ADR-0006's `Generated` as the initial state; do not reproduce the bug | Records invisible to their own filters |
| **V3-CONFLICT-7** | all | v3 is **desktop-only** — no nav breakpoints, no mobile pattern | Define responsive behaviour independently (done in S1) | Unusable on tablet/mobile |
| **V3-CONFLICT-8** ✅ *resolved in S3* | labels | `README.md` documents **7** AI labels; `LABELS` defines **8** (omits `Data Quality`) | Implement all 8 from code, the source of truth | Missing a status treatment |
| **V3-CONFLICT-9** | Decision Cockpit | v3's **D-SUP-1** (supplier delay exposure) is computed entirely from `engine2.js`'s synthetic `SUPPLIERS` / `supplierPerf` / `procurement()` fixtures. The database has **no supplier, purchase-order or delivery-performance entity** — `InventoryItem.LeadTimeDays` is the only related field, and it carries no supplier identity or on-time history | **Do not implement the rule.** Fabricating supplier performance to fill a cockpit tile would put invented figures in front of executives as if they were measured. The remaining five rules ship; D-SUP-1 is tracked as V3-UC08-008 and unblocks once real Fusion Procurement data is integrated | **Medium if ignored** — a plausible-looking but fabricated supplier decision is worse than an absent one |

---

## Slice index

| Slice | Title | Requirement IDs | Status |
|-------|-------|-----------------|--------|
| **S0** | **Dependency & warning hygiene** | V3-QA-005/006 | **Complete** |
| **S1** | **Shell & grouped navigation** | V3-NAV-001/002/003 | **Complete** |
| **S2** | **UC8 Decision Cockpit** | V3-UC08-001…007, 009 | **Complete** (V3-UC08-008 blocked on data) |
| **S3** | **Explainability drawer + label system** | V3-DS-002/006/007, V3-UC0x-002 | **Complete** |
| **S4** | **Identity, roles & authorization** | V3-AUTH-001/002/003/004 | **Complete** (backend; SPA sign-in outstanding) |
| **S5** | **Recommendation records & write path** | V3-GOV-002/003/005/011/012, V3-API-001/002/003 | **Complete** |
| **S6** | **Decision Log & human decisions** | V3-GOV-001/004/006/007, V3-API-002/005, V3-AUTH-004, V3-DS-007, V3-PLAT-007 | **Complete** |
| S7 | Data Health, Lineage, Settings | V3-GOV-008/009/010, V3-PLAT-007 | Planned |
| S8 | Intelligence-screen alignment + perf | V3-UC01..07-001, V3-PERF-001, V3-DS-003 | Planned |
| S9 | Persona, accent, density | V3-NAV-005, V3-DS-004/005 | Planned |
| S10 | Ask Decision Intelligence (AI) | V3-PLAT-001/002 | Planned |
| S11 | Ingestion, Reports, Methodology, Integration | V3-PLAT-003…006 | Planned |
| S12 | E2E, visual, a11y, coverage hardening | V3-QA-001…004 | Planned |
