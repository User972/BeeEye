# Security Policy

## Supported versions

BeeEye is pre-1.0 (see `VersionPrefix` in `Directory.Build.props`). Only the latest `main` receives
security fixes; there are no maintained release branches yet.

| Version | Supported |
| ------- | --------- |
| `main` (0.1.x) | ✅ |
| Older commits  | ❌ |

## Reporting a vulnerability

**Please do not open a public issue for security vulnerabilities.**

Report privately through **GitHub → Security → "Report a vulnerability"** (private advisories), or
contact the maintainers directly at **`<add a security contact address>`**.

Please include:

- the affected component (API, web, ML, infra) and version/commit,
- a description and, if possible, a minimal reproduction,
- the impact you believe it has.

We will acknowledge the report, keep you updated on remediation, and coordinate disclosure.

## Scope & design notes

- BeeEye is **proprietary software deployed into the customer's Azure tenant**; source is not publicly
  distributed. See [docs/architecture/deployment-and-ip-protection.md](docs/architecture/deployment-and-ip-protection.md).
- The threat model and controls are documented in
  [docs/architecture/security-threat-model.md](docs/architecture/security-threat-model.md).
- Secrets come from **Azure Key Vault via managed identity**, never from the repository. `.env` is
  git-ignored; `.env.example` is a non-secret template. Do not commit credentials.
- Oracle Fusion is a **read-only** system of record accessed behind a versioned anti-corruption layer.

> Maintainers: replace `<add a security contact address>` above with a monitored security inbox
> (e.g. `security@…`) before this repository is shared beyond the core team.
