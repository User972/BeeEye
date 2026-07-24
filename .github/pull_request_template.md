<!-- Keep PRs focused and small where possible. See CONTRIBUTING.md and CLAUDE.md. -->

## Summary

<!-- What does this change and why? -->

## Related

<!-- Use case (UC1–UC8), ADR, issue, or tech-debt item (e.g. TD-1). -->

- Use case / ADR / issue:

## Type of change

- [ ] Feature
- [ ] Fix
- [ ] Refactor / cleanup
- [ ] Docs
- [ ] Infra / CI
- [ ] Tests

## How it was tested

<!-- Commands run, scenarios covered, screenshots for UI. -->

## Checklist

- [ ] `dotnet test BeeEye.slnx` passes locally (Docker running for integration tests)
- [ ] `npm run typecheck && npm run test && npm run build` pass in `src/web` (if web changed)
- [ ] `pytest` passes in `ml` (if ML changed)
- [ ] `tests/architecture` pass (if module structure/dependencies changed)
- [ ] Endpoints go through a read/application service — no direct `DbContext` in endpoints
- [ ] `BeeEye.Analytics` still matches the wireframe engine (if analytics changed)
- [ ] Docs / ADRs updated; deferred work logged in `docs/architecture/tech-debt.md`
- [ ] No secrets in the diff
