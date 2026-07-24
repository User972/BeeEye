# Contributing to BeeEye

Thanks for contributing. This guide covers local setup, the change workflow, and the standards CI
enforces. For architecture rules and the repo map, see [CLAUDE.md](CLAUDE.md) and
[docs/architecture/overview.md](docs/architecture/overview.md).

## Prerequisites

- .NET 10 SDK (pinned in `global.json`)
- Node 22
- Python 3.12+
- Docker (for local Postgres and the integration tests)

## Local setup

```bash
docker compose up -d                         # Postgres + Azurite
dotnet build BeeEye.slnx                      # backend
cd src/web && npm ci                          # web deps
cd ml && pip install -e ".[dev]"              # python deps
```

Run the API with `dotnet run --project src/api/BeeEye.Api` (http://localhost:5080) and the SPA with
`npm run dev` in `src/web`.

## Branching & commits

- Branch from `main`: `feat/<slug>`, `fix/<slug>`, `docs/<slug>`, `chore/<slug>`.
- Use [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `test:`, `docs:`, `chore:` …).
- AI-assisted commits include a `Co-Authored-By:` trailer.

## Before opening a PR

Reproduce the four CI jobs locally and make sure they pass:

```bash
dotnet test BeeEye.slnx                                   # Docker must be running (integration tests)
cd src/web && npm run typecheck && npm run test && npm run build
cd ml && pytest
az bicep build --file infra/main.bicep                    # if you touched infra/
```

Also:

- Run `tests/architecture` if you changed module structure or dependencies.
- Update the relevant `docs/adr/` or `docs/architecture/` docs when you change behavior or design;
  record deliberately-deferred work in [`docs/architecture/tech-debt.md`](docs/architecture/tech-debt.md).
- Keep secrets out of the diff. `.env` is git-ignored — configure via `.env.example`.
- A self-review with `/code-review` is encouraged.

## Coding standards

Follow the architecture rules and conventions in [CLAUDE.md](CLAUDE.md), and let `.editorconfig`
drive formatting. In short: endpoints go through read services (not `DbContext`); modules stay
isolated behind published contracts; `BeeEye.Analytics` keeps parity with the wireframe engine;
money is `decimal`; format/parse with `InvariantCulture`.

## CI

The `ci` workflow ([.github/workflows/ci.yml](.github/workflows/ci.yml)) runs four jobs on every PR —
**backend (.NET)**, **web (React/TS)**, **ml (Python)**, and **infra (Bicep)**. All must be green to merge.
