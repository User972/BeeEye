# Tests

The platform's test taxonomy and where each kind lives. Coverage targets (per the
engineering spec): ≥85% line / ≥80% branch overall, ≥95% for critical domain rules,
authorisation, calculations and idempotency.

| Layer | Location | Status | Tooling |
|-------|----------|--------|---------|
| Unit (backend) | `tests/unit` | ✅ scaffolded (18 tests) | xUnit |
| Architecture | `tests/architecture` | ✅ scaffolded (4 tests) | xUnit + reflection |
| Integration | `tests/integration` | planned | xUnit + **Testcontainers** (real PostgreSQL) |
| Contract (Oracle adapters) | `tests/contract` | planned | xUnit + mock Oracle server / fixtures |
| Performance | `tests/performance` | planned | synthetic 100k / 5M / 25M datasets |
| Security | `tests/security` | planned | authz escalation, injection, CSV injection |
| ML | `ml/tests` | ✅ scaffolded (11 tests) | pytest (+ `python tests/*.py` runner) |
| Component / hooks (web) | `src/web/src/**/*.test.tsx` | ✅ scaffolded (6 tests) | Vitest + Testing Library |
| E2E (web) | `tests/e2e` | planned | Playwright |

## Run

```bash
# Backend (unit + architecture)
dotnet test BeeEye.slnx

# Web (typecheck + component tests)
cd src/web && npm run typecheck && npm run test

# ML
cd ml && PYTHONPATH=. python tests/test_metrics.py && PYTHONPATH=. python tests/test_models.py
# or, with the dev extras installed: pytest
```

Integration and persistence-critical tests use Testcontainers against a real
PostgreSQL — not in-memory substitutes — once those modules are implemented.
