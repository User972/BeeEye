# BeeEye — AI Decision Intelligence Platform

A production-grade, Azure-deployable AI decision-intelligence platform for **ADMC**, an
automotive distribution and marketing organisation. BeeEye turns Oracle Fusion ERP/CRM data into
explainable, auditable recommendations across eight business use cases — from sales-forecast accuracy
to inventory-aging risk to an executive decision cockpit.

> **Status: all eight use cases live, on a governed write path.** The full monorepo, all 19
> bounded-context modules, the React shell, the ML baselines, the Azure IaC, and the
> planning/architecture documentation are in place and **build green**. All eight use cases are
> implemented **end-to-end** on real PostgreSQL with a faithful C# port of the wireframe analytics
> engine, live `/api/v1` endpoints and working React screens: **UC1 Order Optimisation**, **UC2 Sales
> Forecasting**, **UC3 Configuration Demand**, **UC4 Procurement**, **UC5 Inventory Aging & Overstock
> Risk**, **UC6 Sales↔After-Sales Correlation**, **UC7 Spare Parts Demand Prediction** and **UC8
> Executive Decision Cockpit**. Around the read-only analytics sits the **governed decision workflow**
> (ADR 0006): frozen, append-only recommendation records; the human decision log (claim → decide →
> sign off → record outcome); **Entra ID authentication with permission-based authorization** (ADR
> 0008); idempotent writes (ADR 0007); and a **shared explainability drawer** that lets every figure,
> forecast and recommendation answer "why?". UC6/UC7 run on a deterministic **synthetic-demo**
> after-sales/parts dataset derived from the real sales history (clearly labelled, never presented as
> real Oracle Fusion data — see
> [data-integration-and-quality](docs/architecture/data-integration-and-quality.md)).

The design language and two of the eight use cases (Sales Forecasting, Inventory Aging) come from the
interactive **Meridian BI** wireframe under [`docs/wireframes/`](docs/wireframes/). The remaining six
use-case workflows are specified under [`docs/product/use-cases/`](docs/product/use-cases/).

## Architecture at a glance

- **Backend** — .NET 10 modular monolith: an ASP.NET Core API host composing 19 bounded-context
  module libraries with strict boundaries (enforced by architecture tests). Provider-neutral AI
  abstraction; GenAI narrates validated metrics but never computes business values.
- **Front end** — React 19 + TypeScript (strict) + Vite + TanStack (Query/Router/Table), a design
  system derived from the wireframe (OKLCH tokens, light/dark), lazy-loaded per-use-case routes.
- **ML** — Python 3.12 batch jobs; seed baselines every model must beat.
- **Data** — Azure Database for PostgreSQL Flexible Server (operational) + ADLS Gen2 (data lake).
- **Integration** — Oracle Fusion is the read-only system of record behind a versioned
  anti-corruption layer.
- **Cloud** — Azure Container Apps + Jobs, Service Bus, Key Vault, Entra ID, App Insights — all via
  Bicep, deployable into the customer's tenant.

See [`docs/architecture/overview.md`](docs/architecture/overview.md) and the
[ADRs](docs/adr/) for the reasoning.

## Repository layout

```
src/
  api/        BeeEye.Api        ASP.NET Core host (composition root, OpenAPI, health)
  modules/    19 bounded contexts (Identity, Forecasting, Inventory, Procurement, …)
  shared/     BeeEye.Shared (pure kernel) + BeeEye.Shared.Web (IModule contract)
  workers/    BeeEye.Workers    generic-host background service host
  web/        React + Vite SPA
ml/           beeeye_ml         Python ML/statistical packages + tests
infra/        Bicep IaC (modules, environments, policies, scripts)
tests/        unit, architecture, integration (Testcontainers Postgres) (+ planned contract/e2e/perf/security)
docs/         wireframes, architecture, adr, product/use-cases
```

## Quick start

Prerequisites: .NET 10 SDK, Node 22, Python 3.12+, Docker.

```bash
# 1. Local infra (PostgreSQL + Azurite)
docker compose up -d

# 2. Backend — build, test, run the API (http://localhost:5080)
dotnet build BeeEye.slnx
dotnet test  BeeEye.slnx
dotnet run --project src/api/BeeEye.Api
#   GET /health/ready · /api/v1/platform/modules · /openapi/v1.json

# 3. Web — the SPA (http://localhost:5173, proxies /api to the host above)
cd src/web && npm install && npm run dev

# 4. ML — seed baselines and metrics
cd ml && PYTHONPATH=. python tests/test_metrics.py
```

## Verified green

| Stack | Command | Result |
|-------|---------|--------|
| Backend build | `dotnet build BeeEye.slnx` | 0 warnings, 0 errors |
| Backend tests | `dotnet test BeeEye.slnx` | 885 passed (384 analytics + 297 unit + 6 architecture + 198 integration) |
| Analytics coverage | coverlet | calc engines 97–100%; overall line-rate maintained |
| API runtime (UC1–UC8) | `dotnet run` vs Postgres | UC5: 291 units / SAR 46.75M; UC6: 5 models, ~24,130 vehicles, 2 high-intensity; UC7: 43 parts × 15 locations; UC8 feed: 5 decisions, 2 critical, 0 gaps |
| Web lint | `npm run lint` | 0 errors (ESLint 9 flat config, type-aware) |
| Web typecheck/build | `npm run build` | 0 errors, code-split per use case |
| Web tests | `npm run test` | 200 passed |
| ML | `pytest` | 15 passed (Croston/SBA/TSB, service-intensity, correlation, …) |
| Infra | `bicep build infra/main.bicep` | 0 errors |

Backend total **885** = 384 analytics · 297 unit · 6 architecture · 198 integration; with the web
suite that is **1085** automated tests, all green. The integration tests use **Testcontainers** (real
PostgreSQL) via `WebApplicationFactory`. Run the full stack locally with `docker compose up -d` then
`dotnet run --project src/api/BeeEye.Api`.

## Delivery roadmap

1. **Foundation** — shell, design system, modules, contracts, IaC, docs. ✅
2. **UC2 + UC5** (wireframed) — Sales Forecasting and Inventory Aging, wired end-to-end. ✅
3. **UC1, UC3, UC4** — order optimisation, configuration demand and procurement intelligence. ✅
4. **UC6, UC7** — after-sales correlation and spare-parts intelligence, on a labelled synthetic dataset. ✅
5. **UC8** — executive cockpit, aggregating the seven contexts via the `IDecisionSignalProvider` contract. ✅
6. **Governance & platform** — frozen append-only recommendation records, the human decision log,
   Entra ID authentication with permission-based authorization, idempotent writes, and the shared
   explainability drawer. ✅

One coherent architecture throughout — no parallel stack per use case.

> **Next (S7–S11):** the Data Health / Lineage / Settings screens, background expiry & supersession
> jobs, and live AI narration inside the drawer —
> tracked slice by slice in [`docs/implementation/v3-progress.md`](docs/implementation/v3-progress.md).

## Documentation

- Executive summary: [docs/investor-brief.md](docs/investor-brief.md)
- Implementation progress (vertical slices S0–S12): [docs/implementation/v3-progress.md](docs/implementation/v3-progress.md)
- Wireframe analysis: [screens](docs/architecture/wireframe-analysis/screen-inventory.md) ·
  [design tokens](docs/architecture/wireframe-analysis/design-token-inventory.md) ·
  [components](docs/architecture/wireframe-analysis/component-inventory.md) ·
  [traceability](docs/architecture/wireframe-analysis/traceability-matrix.md) ·
  [embedded assumptions](docs/architecture/wireframe-analysis/embedded-assumptions.md)
- Use-case specs: [docs/product/use-cases/](docs/product/use-cases/)
- Architecture: [docs/architecture/](docs/architecture/) · Decisions: [docs/adr/](docs/adr/)

This repository contains proprietary vendor source. See
[`docs/architecture/deployment-and-ip-protection.md`](docs/architecture/deployment-and-ip-protection.md)
for the IP-protection release model.
