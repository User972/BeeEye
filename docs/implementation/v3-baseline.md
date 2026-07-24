# V3 Implementation — Pre-Change Baseline

> **Status: frozen pre-change snapshot.** This document records the state of the repository
> **before** any v3-driven change. Every figure here was executed and observed, not estimated.
> Any later regression is measured against this file. **These figures are intentionally historical**
> (e.g. 404 tests, 44 all-`GET` endpoints, no auth, ADR-0006 unimplemented) — for the **current** state
> see [`v3-progress.md`](v3-progress.md) and [`v3-design-traceability.md`](v3-design-traceability.md).

| Field | Value |
|-------|-------|
| Captured | 2026-07-23 |
| Branch | `feat/platform-scaffold` |
| Head commit | `448ad74` (Merge PR #5 — Correlation/Forecasting) |
| Working tree | Clean at capture time |
| Toolchain | .NET 10 SDK (pinned in `global.json`), Node 22 / npm 10.9.3, Docker running |
| Platform | Windows 11, PowerShell + git-bash |

---

## 1. V3 design folder — path resolution

The brief referred to `docs/wifreframes-v3`. **That path does not exist.**

| Candidate | Exists | Tracked in git |
|-----------|--------|----------------|
| `docs/wifreframes-v3` (misspelled) | No | No |
| **`docs/wireframes-v3`** | **Yes** | **Yes — commit `cc8d073` "High Fidelity Wireframe V3 added"** |

**Resolution:** the committed folder is spelled correctly. No rename is required and no references
need updating. All v3 work uses `docs/wireframes-v3`. The misspelling exists only in the task brief.

### V3 vs V1 content delta (checksum-verified)

| File | V1 | V3 | Result |
|------|----|----|--------|
| `engine.js` | 59,519 B | 59,519 B | **Identical** (md5 match) |
| `support.js` | 66,404 B | 66,404 B | **Identical** (md5 match) |
| `.env.example` | 1,669 B | 1,669 B | **Identical** (md5 match) |
| `data/dataset.js`, `data/sales.json`, `data/inventory.json` | — | — | **Identical** (byte-for-byte size match) |
| `Meridian BI.dc.html` | 246,955 B | **518,334 B** | Changed — 2.1× larger |
| `engine2.js` | absent | **60,909 B** | **New in v3** |
| `README.md` | 4,690 B | 7,184 B | Changed — documents the 8-use-case extension |
| `docs/ACCEPTANCE_REPORT.md` | absent | present | **New in v3** |
| `docs/DEMO_DATA_CATALOGUE.md` | absent | present | **New in v3** |
| `docs/USE_CASE_MAPPING.md` | absent | present | **New in v3** |

**Conclusion: v3 is purely additive.** The UC2/UC5 analytics contract (`engine.js`) is unchanged, so
the existing `BeeEye.Analytics` parity port remains valid and is **not** at risk from v3. All new
computation lives in `engine2.js`.

---

## 2. Build

### 2.1 .NET

```
dotnet build BeeEye.slnx
```

**Result: succeeded — 0 errors, 23 warnings, 25.73 s.**

All 14 projects built, including `BeeEye.Api`, 19 module libraries, 4 shared libraries and 4 test
projects.

#### Pre-existing warnings (NOT caused by v3 work)

| Count | Code | Location | Note |
|-------|------|----------|------|
| 3 | `CS8604` possible null reference argument | `src/shared/BeeEye.Analytics/Configuration/ConfigurationDemand.cs:27,62` | Nullable-flow gap in `MonthKey.Trailing` / `ConfigDemandResult` construction |
| 2 | `CS8604` possible null reference argument | `src/modules/Forecasting/Application/ForecastingReadService.cs:136` | `MonthKey.Range(from, to)` args not null-checked |
| 18 | `NU1903` **high-severity vulnerability** | `Microsoft.OpenApi` 2.0.0; `System.Security.Cryptography.Xml` 9.0.0 | See §7 — security-relevant, tracked as a risk |

The build is **not** warning-free. The Definition of Done for the v3 programme requires "no
unexplained build warnings"; these 23 are hereby explained and attributed to the pre-existing
baseline. They are candidates for a hygiene slice, not v3 regressions.

### 2.2 Web

```
cd src/web && npm run typecheck && npm run lint && npm run build
```

| Step | Result |
|------|--------|
| `tsc --noEmit` (strict) | **Clean — 0 errors** |
| `eslint .` | **Clean — 0 errors, 0 warnings** |
| `vite build` | **Succeeded — 205 modules transformed, 1.89 s** |

---

## 3. Test baseline — 404 tests, 0 failures

### 3.1 .NET (`dotnet test BeeEye.slnx --no-build`)

| Project | Passed | Failed | Skipped | Total | Duration |
|---------|--------|--------|---------|-------|----------|
| `BeeEye.UnitTests` | 18 | 0 | 0 | 18 | 75 ms |
| `BeeEye.Analytics.Tests` | 332 | 0 | 0 | 332 | 207 ms |
| `BeeEye.ArchitectureTests` | 4 | 0 | 0 | 4 | 12 s |
| `BeeEye.IntegrationTests` (Testcontainers Postgres) | 33 | 0 | 0 | 33 | 29 s |
| **Backend total** | **387** | **0** | **0** | **387** | — |

### 3.2 Web (`npm run test` — Vitest 3.2.7)

| File | Tests |
|------|-------|
| `src/config/navigation.test.ts` | 4 |
| `src/lib/format.test.ts` | 7 |
| `src/components/ui/Badge.test.tsx` | 2 |
| `src/components/domain/SyntheticBanner.test.tsx` | 1 |
| `src/components/charts/charts.test.tsx` | 3 |
| **Web total** | **17** (5 files, 3.71 s) |

### 3.3 Combined

**404 tests, 404 passing, 0 failing, 0 skipped.**

- **Pre-existing failures: none.** The baseline is green.
- **Environmental failures: none** (Docker was running; integration tests executed rather than skipped).
- **Known flaky tests: none observed** in this run. Not proven stable across repeated runs — only one
  full run was executed.

### 3.4 Test-coverage gaps (structural, not numeric)

No coverage tooling or threshold is configured in either stack, so no coverage percentage can be
reported. The following test *categories* are **entirely absent** from the repository:

| Category | Present |
|----------|---------|
| Unit (backend) | Yes |
| Architecture / boundary | Yes (4 tests) |
| Integration + Testcontainers | Yes (33 tests) |
| Component (web) | Yes, but only 5 files for 10 pages + 13 components |
| **End-to-end / browser** | **No** |
| **Visual regression** | **No** |
| **Accessibility (automated)** | **No** |
| **Contract tests** | **No** |
| **Performance tests** | **No** |
| **Security tests** | **No** |
| **Coverage tooling / threshold** | **No** |

Web component coverage is thin: `executive-cockpit`, `sales-forecasting`, `inventory-intelligence`,
`order-optimisation`, `configuration-demand`, `procurement`, `after-sales`, `spare-parts`,
`data-management` and `platform-settings` have **no** page-level tests.

---

## 4. Runtime baseline

### 4.1 Startup

`dotnet run --project src/api/BeeEye.Api` against the `docker compose` Postgres:

- Applied EF migrations on boot (`__EFMigrationsHistory` checked, `Migrate()` executed).
- **`/health/ready` returned `Healthy` 8 s after launch.**
- Documented dev instructions in `CLAUDE.md` / `README.md` work as written.

### 4.2 Migrations

Two migrations exist:

1. `20260722070828_InitialCreate`
2. `20260722140036_AfterSalesAndParts`

**Clean-database application verified** — the 33 Testcontainers integration tests each provision a
fresh Postgres 16 container and migrate from empty, and all passed.

### 4.3 API surface — 44 endpoints, all read-only

Enumerated from the live OpenAPI document at `/openapi/v1.json`:

**44 paths. Every one is `GET`. There are zero `POST`/`PUT`/`PATCH`/`DELETE` endpoints in the entire
application.** This is the single most consequential baseline fact for v3 (see §6).

### 4.4 API latency (localhost, warm Postgres, single-user)

Measured with `curl -w "%{time_total}"`; "cold" is the first call after boot, "warm" the second.

| Endpoint | Cold | Warm | Response size |
|----------|------|------|---------------|
| `/health/ready` | 4 ms | 4 ms | 7 B |
| `/api/v1/forecasting/forecast` | 50 ms | **16 ms** | 6.0 kB |
| `/api/v1/recommendations/order-optimisation` | 39 ms | **12 ms** | 6.5 kB |
| `/api/v1/procurement/recommendations` | 126 ms | **33 ms** | 8.7 kB |
| `/api/v1/sales-actuals/config-demand/summary` | 144 ms | **34 ms** | 496 B |
| `/api/v1/inventory/items?pageSize=50` | 79 ms | **39 ms** | 18.7 kB |
| `/api/v1/inventory/summary` | 185 ms | **50 ms** | 4.3 kB |
| `/api/v1/spare-parts/demand/summary` | 518 ms | **275 ms** | 678 B |
| `/api/v1/after-sales/service-intensity/summary` | 615 ms | **669 ms** | 479 B |

**Observation.** The two UC6/UC7 endpoints are 8–40× slower than every other endpoint while returning
the *smallest* payloads (< 700 B). `after-sales/service-intensity/summary` does not improve on the
warm call, indicating per-request recomputation over the full sales/service history rather than a
cold-cache effect. This is a pre-existing performance characteristic, recorded here so that v3 work is
not blamed for it — and flagged as an optimisation candidate, since v3's Decision Cockpit aggregates
across **all** modules and would inherit this cost.

### 4.5 Frontend asset baseline (`vite build`, production)

| Asset | Raw | Gzip |
|-------|-----|------|
| `index.html` | 0.98 kB | 0.55 kB |
| `assets/index-*.css` (entire design system) | **11.29 kB** | **3.01 kB** |
| `assets/index-*.js` (main bundle) | **323.23 kB** | **102.16 kB** |
| `assets/inventory-intelligence-*.js` (largest route chunk) | 59.83 kB | 16.56 kB |
| `assets/spare-parts-*.js` | 12.34 kB | 3.87 kB |
| `assets/client-*.js` | 12.28 kB | 4.44 kB |
| `assets/after-sales-*.js` | 8.97 kB | 3.00 kB |
| `assets/sales-forecasting-*.js` | 8.61 kB | 3.23 kB |
| Remaining 14 chunks | each < 6 kB | — |

Routes are already code-split per page and lazy-loaded. Total production output ≈ **460 kB raw /
≈ 145 kB gzip**.

### 4.6 Page load times

**Not measured.** No Lighthouse, Playwright or equivalent tooling exists in the repository, and none
was introduced during baselining to avoid changing the baseline being measured. Recorded as a gap;
page-load measurement is proposed as part of the performance-hardening slice.

---

## 5. Static analysis

| Tool | Configured | Result |
|------|-----------|--------|
| `tsc --noEmit` (strict) | Yes | Clean |
| ESLint 9 (flat config, typescript-eslint) | Yes | Clean |
| .NET analyzers (`Nullable` enabled) | Yes | 5 `CS8604` warnings (§2.1) |
| NuGet audit (`NU1903`) | Yes (implicit) | 18 high-severity advisories (§7) |
| Dedicated SAST (CodeQL etc.) | **No** | — |
| Web accessibility linting (`eslint-plugin-jsx-a11y`) | **No** | — |

---

## 6. Architectural baseline facts that constrain v3

These are **observed**, not assumed, and they set the sequencing of the implementation plan.

1. **The application is entirely read-only.** 44 endpoints, all `GET` (§4.3). There is no command
   handling, no write path, no transaction boundary beyond EF's implicit one, no idempotency
   mechanism and no outbox.

2. **There is no authentication or authorization.** No authentication scheme, no `RequireAuthorization`,
   no policies, no roles, no user identity anywhere in the composition root.

3. **There is no multi-tenancy.** No tenant entity, no tenant column, no query filter, no tenant scoping.

4. **The persistence model has 9 entities** — `IngestionBatch`, `InventoryItem`, `Part`,
   `PartCompatibility`, `PartSupersession`, `PartUsage`, `SalesFact`, `ServiceEvent`, `VehicleSale`.
   None of them is a `Recommendation`, `ManagementDecision`, `RecStatusEvent`, `ApprovalStep` or
   `ActionOutcome`.

5. **ADR-0006 is Accepted but unimplemented.** `docs/adr/0006-recommendation-decision-workflow.md`
   (status **Accepted**, dated 2026-07-22) specifies in full the append-only recommendation/decision
   record model and lifecycle state machine — precisely the architecture v3's Governance group
   requires. **No code implements it.** This is the largest single gap between documentation and
   implementation, and it is the reason the first v3 slice is a backend slice rather than a
   design-token slice.

6. **Design tokens are already aligned with v3.** `src/web/src/styles/tokens.css` and the v3
   prototype's `:root` block use the same OKLCH values for `--bg`, `--surface`, `--nav-bg`,
   `--primary`, `--risk-*`, `--ai-1/2`, `--shadow-*`, `--radius`, `--gap` and `--card-pad`. The
   conventional "design tokens first" opening slice is therefore largely a no-op and would deliver no
   user-visible value.

7. **Navigation shape differs.** Current: 3 sections (`overview`, `intelligence`, `platform`) /
   10 items. V3: 6 groups (`Executive`, `Sales Intelligence`, `Supply Intelligence`,
   `After-Sales Intelligence`, `Governance`, `Platform`) / 18 items, with phase labels and live badge
   counts.

---

## 7. Pre-existing security findings

Recorded so they are neither attributed to v3 nor silently inherited.

| ID | Finding | Severity | Source |
|----|---------|----------|--------|
| B-SEC-1 | `Microsoft.OpenApi` 2.0.0 — GHSA-v5pm-xwqc-g5wc | High | `NU1903`, affects `BeeEye.Api` + `BeeEye.IntegrationTests` |
| B-SEC-2 | `System.Security.Cryptography.Xml` 9.0.0 — 7 advisories (GHSA-23rf-6693-g89p, -37gx-xxp4-5rgx, -8q5v-6pqq-x66h, -cvvh-rhrc-wg4q, -g8r8-53c2-pm3f, -mmjf-rqrv-855v, -w3x6-4m5h-cxqf) | High | `NU1903`, affects `BeeEye.Persistence` |
| B-SEC-3 | No authentication or authorization anywhere in the application | — | Observed (§6.2) |
| B-SEC-4 | No multi-tenancy or data isolation | — | Observed (§6.3) |

B-SEC-1 and B-SEC-2 are transitive dependency advisories with pinned central versions in
`Directory.Packages.props`; they are independent of v3 and should be remediated in a hygiene slice.
B-SEC-3 and B-SEC-4 are **not defects** — the application is a read-only analytics platform over a
single tenant's data and never claimed otherwise. They become material only when v3 introduces write
paths, which is exactly why the first slice addresses them.

---

## 8. Baseline summary

| Dimension | Baseline |
|-----------|----------|
| .NET build | Succeeded, 0 errors, 23 warnings |
| Web typecheck | Clean |
| Web lint | Clean |
| Web build | Succeeded, 205 modules |
| Tests | **404 / 404 passing** (387 backend + 17 web) |
| Pre-existing test failures | **None** |
| Migrations on clean DB | Verified via Testcontainers |
| API startup | Healthy in 8 s |
| API endpoints | 44, **all GET** |
| Slowest endpoint | `after-sales/service-intensity/summary` @ 669 ms warm |
| Main JS bundle | 323.23 kB raw / 102.16 kB gzip |
| CSS bundle | 11.29 kB raw / 3.01 kB gzip |
| E2E / visual / a11y / perf / security tests | **None exist** |
| Coverage tooling | **None configured** |

The repository is in a healthy, green, reproducible state. Every subsequent v3 slice must leave these
figures at least as good, and any deviation must be explained against this document.
