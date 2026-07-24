# BeeEye web

React 19 + TypeScript (strict) + Vite SPA for the BeeEye decision-intelligence platform.

```bash
npm ci
npm run dev        # http://localhost:5173, proxies /api and /health to the .NET host on :5080
npm run lint       # eslint .
npm run typecheck  # tsc --noEmit
npm run test       # vitest run
npm run build      # tsc --noEmit && vite build
```

## Authentication modes (S4b / ADR 0008)

The SPA authenticates in one of two modes, selected by configuration — **not** by guessing from the
environment. Copy [`.env.example`](.env.example) to `.env` (git-ignored) and set the values you need.

| Mode | When | Behaviour |
|------|------|-----------|
| **`local`** | The default when `VITE_AAD_CLIENT_ID` is absent | No bearer token, no sign-in gate. Mirrors the backend's LocalDev posture, so `npm run dev` needs **no Entra tenant**. The app renders anonymously exactly as it did before sign-in existed. |
| **`entra`** | A deployed build, or a staging-like local run | Real MSAL (PKCE, redirect flow, `sessionStorage` cache). Every API call carries an Entra access token; an anonymous user is routed to sign-in; a 401 triggers a single silent refresh + replay. |

Mode is set by `VITE_AUTH_MODE` (`entra` | `local`), defaulting to `local` when no client id is set.

### Environment variables

| Variable | Required in `entra` | Purpose |
|----------|:---:|---------|
| `VITE_API_BASE_URL` | no | Blank in dev to use the Vite proxy. |
| `VITE_AUTH_MODE` | — | `entra` \| `local`. Defaults to `local` when `VITE_AAD_CLIENT_ID` is absent. |
| `VITE_AAD_CLIENT_ID` | ✅ | SPA public-client (PKCE) application id. |
| `VITE_AAD_AUTHORITY` | ✅ | e.g. `https://login.microsoftonline.com/{tenantId}/v2.0`. **Must equal** the backend's `Auth:Authority`. |
| `VITE_AAD_API_SCOPE` | ✅ | The API scope, e.g. `api://{api-app-id}/access_as_user`. |
| `VITE_AAD_REDIRECT_URI` | no | Defaults to `window.location.origin`. |

### Production build guard

A production `vite build` **fails fast** if `VITE_AUTH_MODE` is `entra` (or is inferred from a client
id) but any of `VITE_AAD_CLIENT_ID` / `VITE_AAD_AUTHORITY` / `VITE_AAD_API_SCOPE` is missing — a
deployment must never boot into accidental anonymous mode. `local` builds (CI, dev) are unaffected.

### Staging-like local run (verify `entra` mode end-to-end, no live tenant needed for the SPA UI)

This exercises the real MSAL wiring against an Entra tenant with the secure backend posture:

1. **Backend** — run the API host in `EntraId` mode with reads required:
   ```bash
   ASPNETCORE_ENVIRONMENT=Staging \
   Auth__Provider=EntraId \
   Auth__Authority="https://login.microsoftonline.com/{tenantId}/v2.0" \
   Auth__Audience="api://{api-app-id}" \
   Auth__RequireAuthenticatedReads=true \
   dotnet run --project src/api/BeeEye.Api
   ```
   (The host aborts at boot if `Authority`/`Audience` are missing — that is the intended fail-fast.)
2. **SPA** — set `.env` to `entra` mode with the matching `VITE_AAD_*` values (the `VITE_AAD_AUTHORITY`
   must equal the backend `Auth:Authority`, and `VITE_AAD_API_SCOPE` must be a scope the API app
   exposes), then `npm run dev`.
3. Confirm: an anonymous visit is routed to sign-in; after sign-in the account chip shows the name from
   `/identity/me`; reads and writes succeed with a bearer attached; sign-out lands on the signed-out
   state.

The Entra app registrations (SPA public PKCE client, API app exposing a scope, app roles for
Executive / Analyst / IT personas) are provisioned per
[`docs/architecture/deployment-and-ip-protection.md`](../../docs/architecture/deployment-and-ip-protection.md) §7.

## Testing notes

- **Unit / component** — Vitest + React Testing Library (jsdom). The suite mocks the global `fetch` and
  returns real `Response` objects; it does **not** mock `apiGet`/`apiPost` or use MSW. MSAL is mocked at
  the module boundary — no unit/component test hits a live IdP.
- Auth-specific suites: `src/lib/api/client.test.ts` (transport seam, 401 recovery, idempotency-key
  preservation), `src/lib/auth/config.test.ts`, `src/lib/auth/msalBridge.test.ts`, and
  `src/components/layout/signin-flow.test.tsx` (header + route gate, local-mode parity).

### Coverage (V3-QA-004)

```bash
npm run test:coverage   # vitest run --coverage; enforces the threshold floor, reports text/html/lcov
```
Thresholds live in `vite.config.ts` (`test.coverage.thresholds`). They sit just below the current
measured coverage so the gate can only ratchet **up** — raise them as coverage grows, never lower them.
The HTML report is written to `coverage/` (git-ignored); CI uploads `lcov.info`.

### Accessibility (V3-QA-003)

- Component-level (`src/components/a11y.test.tsx`, in the vitest suite): `vitest-axe` over the
  design-system primitives and the populated drawer, **both themes**, asserting zero serious/critical.
  `color-contrast` is disabled here — jsdom has no layout — and is checked at the route level instead.
- Route-level (`e2e/a11y.spec.ts`): `@axe-core/playwright` over every route and the drawer open states,
  in a real browser. Enforces zero serious/critical **except** two baselined pre-existing rules —
  `color-contrast` (v3 palette muted-text debt) and `scrollable-region-focusable` — each documented in
  the spec and tracked (R-11) for separate remediation. Not a blanket disable; remove each id as fixed.

### End-to-end & visual regression (Playwright — V3-QA-001 / V3-QA-002)

```bash
npm run e2e                       # all Playwright specs (7 viewport projects)
npm run e2e -- --grep-invert @visual   # functional + a11y only
npm run e2e -- --grep @visual          # visual only
npm run e2e:update                # (re)generate the @visual baselines
npx playwright show-report        # open the last HTML report
```

- The suite is deterministic: the API runs in **LocalDev** (all roles, no tenant), the SPA is built in
  **`local`** mode, and `vite preview` proxies `/api` to the API so they share an origin. Playwright's
  `webServer` boots both; locally, start Docker (for the seeded Postgres) first.
- **Visual baselines** (`e2e/__screenshots__/`) are **platform-pinned** — generate them on the CI runner
  (Linux), never on macOS/Windows, or every diff churns:
  ```bash
  # in the CI runner / Playwright Linux container:
  npm run e2e:update && git add e2e/__screenshots__ && git commit
  ```
  Until the baselines are committed, the `@visual` CI step is red — this is the expected one-time
  bootstrap, not a defect. An unreviewed pixel change fails CI thereafter (like the OpenAPI drift gate).
- **Flake policy**: no `waitForTimeout`; web-first assertions only; CI retries capped at one
  (`on-first-retry` trace). A test that passes only on retry is quarantined and tracked, not left green.

### CI matrix

| Job | Runs |
|-----|------|
| `web` | lint · typecheck · build · **coverage-gated** vitest · uploads `lcov` |
| `e2e` | seeded Postgres → build API+SPA → install Chromium → functional + a11y gate → visual gate → uploads report/traces/diffs on failure |
| `backend` / `integration` / `openapi` / `ml` / `infra` | unchanged |

## OpenAPI contract

`src/lib/api/schema.d.ts` is generated from the committed `openapi/openapi.json` by `npm run gen:api`.
If a backend endpoint or DTO changes, regenerate and commit both in the same change — the CI `openapi`
job fails on drift.
