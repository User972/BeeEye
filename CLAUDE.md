# CLAUDE.md

Guidance for Claude Code (and other AI agents) working in this repository. Keep it accurate:
the `/code-review` conventions pass reads this file and flags changes that violate the rules below.

## What this is

**BeeEye** — an AI decision-intelligence platform for **ADMC** (automotive distribution). A .NET
modular monolith + React SPA + Python ML, deployable to Azure. The current implementation is
**read-only analytics**: **UC2 (Sales Forecasting)** and **UC5 (Inventory Aging & Overstock Risk)**
are live end-to-end; the other bounded contexts are scaffolded. See [README.md](README.md) and
[docs/architecture/overview.md](docs/architecture/overview.md).

## Repository map

- `src/api/BeeEye.Api` — ASP.NET Core **minimal-API host** (composition root, OpenAPI, health). No business logic.
- `src/modules/<Context>` — 19 bounded-context module libraries; each implements `IModule`. Live: Forecasting, Inventory.
- `src/shared/BeeEye.Analytics` — pure numeric engine (forecasting, demand, inventory risk). **Faithful C# port of `docs/wireframes/engine.js`.**
- `src/shared/BeeEye.Shared` — dependency-free kernel (`Money`, `Result`, `Paging`, `MonthKey`, …).
- `src/shared/BeeEye.Shared.Web` — the `IModule` contract.
- `src/shared/BeeEye.Persistence` — EF Core `BeeEyeDbContext`, entities, migrations, sample-data importer.
- `src/web` — React 19 + TypeScript (strict) + Vite + TanStack Query/Router/Table.
- `ml/beeeye_ml` — Python baseline models (the bar the production ML must beat).
- `tests/` — `unit`, `architecture` (boundary enforcement), `integration` (Testcontainers Postgres).
- `docs/` — `adr/`, `architecture/`, `product/use-cases/`, `wireframes/`. Pending work: [`docs/architecture/tech-debt.md`](docs/architecture/tech-debt.md).

## Build / test / run

Tooling: **.NET 10 SDK** (pinned in `global.json`), **Node 22**, **Python 3.12+**, **Docker**.

```bash
docker compose up -d                 # local Postgres + Azurite

# .NET
dotnet build BeeEye.slnx
dotnet test  BeeEye.slnx             # whole suite — integration tests need Docker running
dotnet test  tests/unit/BeeEye.Analytics.Tests/BeeEye.Analytics.Tests.csproj   # a single project
dotnet run --project src/api/BeeEye.Api      # API on http://localhost:5080

# Web
cd src/web && npm ci
npm run typecheck && npm run test && npm run build

# Python
cd ml && pip install -e ".[dev]" && pytest
```

`dotnet test` accepts a solution **or a single** project — passing several project paths in one call errors.

## Architecture rules (enforced / expected)

1. **Layering.** HTTP endpoints call an application/read service; they must **not** use
   `BeeEyeDbContext` directly. Data access lives in `*ReadService` classes.
2. **No generic repository** over EF Core (`DbContext` is the unit-of-work, `DbSet` the repository).
   The read-store seam is deferred — see `docs/architecture/tech-debt.md` (TD-1).
3. **Module isolation.** A module never references another module's implementation types;
   cross-context communication goes through published contracts. Enforced by `tests/architecture` —
   run it after changing module structure.
4. **New endpoints** belong to a module implementing `IModule` (`Name`, `RoutePrefix`, `Description`,
   `Status`, `RegisterServices`, `MapEndpoints`), mounted under `/api/v1/{RoutePrefix}`.
5. **Analytics parity.** `BeeEye.Analytics` mirrors `docs/wireframes/engine.js`. When changing a
   formula, preserve parity and keep the unit tests in `tests/unit/BeeEye.Analytics.Tests` green.
6. **Money is `decimal`, never floating point** (`BeeEye.Shared.Primitives.Money`); persist with explicit precision.
7. **Culture-invariant.** `InvariantGlobalization` is on; format/parse with `CultureInfo.InvariantCulture`
   (month keys, numbers, money).

## Conventions

- Central package versions in `Directory.Packages.props`; shared build props in `Directory.Build.props`
  — do **not** redeclare `TargetFramework`/`Nullable` in a `.csproj`.
- Nullable + ImplicitUsings enabled; file-scoped namespaces; `record` for DTOs/contracts.
- Branch `feat/…`, `fix/…`, `docs/…`; Conventional Commits. Keep all CI jobs (backend, web, ml, infra) green.
- Never commit secrets; copy `.env.example` → `.env` for local dev (`.env` is git-ignored).

## Gotchas

- Integration tests spin up a real Postgres via Testcontainers → **Docker must be running**.
- API startup migrates + seeds and **swallows DB failures** (logs a warning) so it can boot without
  Postgres; `/health/ready` reports actual DB connectivity.
- Sample data is embedded and **idempotent by file checksum** (see `SampleDataImporter`).
