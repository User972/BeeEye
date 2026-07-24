# ADR 0008 — Authentication & Permission-Based Authorization

> Purpose: fix how BeeEye establishes *who* is calling and *what they may do*, so that every
> state-changing operation can name the accountable human — the precondition
> [ADR 0006](0006-recommendation-decision-workflow.md) depends on and which no code currently satisfies.

| Field | Value |
|-------|-------|
| Status | **Accepted &amp; Implemented** (S4 backend enforced; S4b SPA sign-in shipped) |
| Date | 2026-07-23 |
| Deciders | Platform Architecture, Security Architecture, ADMC IT |
| Scope | Platform-wide; binds the API host, all 19 bounded contexts and the SPA |
| Owning contexts | `Identity`, `PlatformAdministration`, `Audit` |
| Supersedes / superseded by | — |

---

## 1. Context & Problem

The application is, at the time of this decision, **entirely anonymous**. Verified against the live
OpenAPI document: **44 endpoints, every one a `GET`, none requiring authentication**. There is no
authentication scheme, no authorization policy, no role model and no user identity anywhere in the
composition root.

That was defensible while BeeEye was read-only analytics over a single tenant's sample data. It stops
being defensible the moment the v3 designs are implemented, because:

1. **ADR 0006 requires a named human.** Its central claim is that the record "proves a *named human*
   made the binding call; the algorithm only advised." A `ManagementDecision` carries `decided_by`.
   Without identity, every decision record would be unattributable — which defeats the ADR's entire
   purpose (audit, liability separation, the learning loop).
2. **Segregation of duties is a stated requirement.** ADR 0006 and the threat model both require that
   no single actor can both author and approve an action. That is unenforceable without identity.
3. **v3 introduces write paths.** The Decision Log, settings persistence and data ingestion all mutate
   state. Anonymous mutation of business records is not acceptable.

[../architecture/security-threat-model.md](../architecture/security-threat-model.md) §2–§3 already
specifies the intended model in detail. **No ADR ratifies it**, so it has the status of a design note
rather than a binding decision. This ADR ratifies it and records the implementation-shaping choices
the threat model leaves open — principally how the platform behaves *before* ADMC's Entra tenant is
available, without opening a hole.

### Decision drivers

| Driver | Why it matters here |
|--------|--------------------|
| **Accountability** | A decision worth millions of SAR must name a person, not a session. |
| **No local user store** | ADMC owns identity. BeeEye must not become a credential store — that is a liability, not a feature. |
| **Developer experience** | The stack must run locally without an Entra tenant, or day-to-day work stops. |
| **Fail closed** | A misconfiguration must refuse to start, never silently run unauthenticated. |
| **Stability under role churn** | ADMC will re-cut roles. Handlers must not break when they do. |
| **Backward compatibility** | 44 existing read endpoints and their consumers must not break on the day auth lands. |

---

## 2. Decision

**BeeEye delegates authentication to Microsoft Entra ID and enforces *permission-based* authorization
server-side at the API boundary. Authorization is always evaluated — there is no code path that skips
it. A guarded, development-only authentication provider supplies a local principal so the stack runs
without an Entra tenant, and the host refuses to boot if that provider is active outside Development.**

Concretely:

### 2.1 Authentication

- **Interactive users** authenticate against Entra ID via OpenID Connect / OAuth2
  **Authorization Code + PKCE**. No implicit flow. No client secret in the SPA. Tokens are held in
  memory, never in `localStorage`.
- The API validates **every** bearer token before any handler runs: signature against the published
  JWKS, exact `iss`, `aud` equal to BeeEye's registered API app-id URI, `exp`/`nbf` with ≤ 2 minutes
  clock skew, and the signing algorithm pinned to an asymmetric family so `alg: none` and symmetric
  downgrades are rejected.
- **Missing or invalid token → `401`. Valid token, insufficient authority → `403`.** Never `200` with
  an empty body: silently returning nothing hides an authorization defect and leaks nothing useful to
  a legitimate user either.

### 2.2 Authorization is permission-based, not role-based

Handlers and policies **always test a fine-grained permission** (`resource.action`), never a role
name. Roles exist only as a convenient bundle of permissions. When ADMC re-cuts roles, the mapping
table changes and **no endpoint changes**.

The permission catalogue and the role → permission mapping are those in the threat model §3.1–§3.2 and
are implemented as a single strongly-typed source of truth so the code and the document cannot drift.

**Approval permissions are separated from authoring permissions**, so no single role can both invent
and approve an action (`recommendation.approve` and `procurement.approve` are Executive-only;
`procurement.propose` is Analyst-only).

### 2.3 The development provider, and the three guards that fence it

A `LocalDev` authentication provider issues a principal for a configured developer identity and role
set, so the stack runs with no Entra tenant. It is fenced by **three independent guards, all of which
must permit it**:

1. Registration is gated on `IHostEnvironment.IsDevelopment()`.
2. An explicit opt-in setting, `Auth:Provider = "LocalDev"`, which is **not** the deployed default.
3. A **startup assertion that throws and aborts boot** if the local provider is selected while the
   environment is not Development.

The critical property: **the third guard fails the process rather than falling back**. A
misconfigured deployment does not quietly run with a fake identity — it does not run at all.

Authorization is enforced identically under both providers. The development provider changes *who you
are*, never *whether you are checked*. There is deliberately no "authorization disabled" mode,
because such a mode inevitably reaches production.

### 2.4 Read endpoints during rollout

The 44 existing read endpoints predate authentication and have live consumers. When this flag was
introduced the SPA had no sign-in flow, so requiring a token on every read the day auth landed would
have broken it. The SPA sign-in flow now ships (S4b / `V3-AUTH-001`), so deployed environments can
require authenticated reads; Development stays relaxed so a local run needs no Entra tenant.

The compatibility mechanism is a single setting, `Auth:RequireAuthenticatedReads`:

| Environment | Default | Effect |
|-------------|---------|--------|
| Development | `false` | Read endpoints remain anonymous; writes still require authentication. |
| Staging / Production | **`true`** | Read endpoints require an authenticated principal with the relevant `*.view` permission. |

**This flag never applies to state-changing operations.** Every write requires an authenticated
principal and an explicit permission, unconditionally, in every environment. The flag existed solely to
stage the migration of *reads* until the SPA could sign users in. That condition is now met
(`V3-AUTH-001`, S4b): deployed hosts default to the secure `true` and no longer depend on the
relaxation. The flag is retained deliberately — it keeps the Development relaxation that lets a local
run work without an Entra tenant, and it stays as a safety net (with the `RelaxedReadPostureAnnouncer`
warning on any deployed host that lowers it). Retiring it entirely is a separate backend follow-up.

### 2.5 Service-to-service

The API host and Python jobs authenticate to PostgreSQL, storage, Key Vault and the message broker
using **Azure managed identities** — no connection-string passwords, no shared keys in images. Nothing
sensitive is baked into a container.

---

## 3. Considered Options

| Option | Summary | Verdict |
|--------|---------|---------|
| **A. Local user store** | BeeEye owns users, passwords, MFA, lockout, reset. | **Rejected** |
| **B. Role-based checks in handlers** | `[Authorize(Roles = "Analyst")]` at each endpoint. | **Rejected** |
| **C. Entra ID + permission-based policies** (this ADR) | Delegate identity; test fine-grained permissions; roles are a mapping. | **Accepted** |
| **D. Defer auth until the Decision Log needs it** | Keep the platform anonymous; add identity with S6. | **Rejected** |

**Why A is rejected.** Owning credentials means owning password policy, MFA, lockout, breach
notification and a permanently attractive attack surface — for an enterprise that already operates
Entra ID and whose IT function owns conditional access. It duplicates a solved problem and inherits
its liabilities. ADMC's MFA and conditional-access policy stays in ADMC's control, where it belongs.

**Why B is rejected.** Role checks scattered through handlers ossify the role model: every re-cut of
ADMC's roles becomes a code change across 19 contexts, and each such change is an opportunity to get
an authorization check wrong. Permission-based policies confine that churn to one mapping table.
Roles also express *who someone is* rather than *what the operation needs*, which is the wrong
question at an endpoint.

**Why D is rejected.** It sequences the highest-risk work last. Retrofitting identity onto an existing
write path means every already-written record has no accountable actor, and backfilling attribution
after the fact is impossible. Identity must precede the first mutation, not follow it.

---

## 4. Consequences

### Positive

- ADR 0006 becomes implementable: a decision can name its human, and segregation of duties is
  enforceable.
- Authorization survives role churn — re-cutting roles touches one table, not 19 contexts.
- No credential storage, so no credential breach surface.
- Misconfiguration fails loudly at boot instead of silently running open.
- The SPA can use the same permission set to *hide* controls, with the server remaining the only
  authority that *grants* access.
- The SPA sign-in flow (MSAL PKCE, silent token acquisition and refresh, one bounded 401 retry that
  preserves the idempotency key, and sign-out) is delivered in S4b (`V3-AUTH-001`), so deployed hosts
  can require authenticated reads instead of serving them anonymously.

### Negative / costs

- A hard dependency on Entra availability for interactive login in deployed environments.
- Two authentication paths to keep tested (Entra, LocalDev). Mitigated by authorization being
  identical under both, and by a test asserting the local provider cannot be selected outside
  Development.
- `Auth:RequireAuthenticatedReads` is a staging flag, and staging flags rot. Its named removal
  condition — the SPA sign-in flow shipping — is now met (S4b), so the deployed relaxation is no longer
  needed; the flag is kept only for the Development relaxation and as a safety net. Retiring it entirely
  is tracked as a separate backend follow-up.
- Entra sign-in adds the MSAL browser library to the SPA bundle and a hard dependency on Entra for
  interactive login. (The token acquisition, refresh and 401-recovery path itself is delivered in S4b,
  not outstanding.)

### Neutral

- Data-scope filtering (*which rows*, threat model §3.3) is a separate concern from authorization
  (*which actions*) and is **not** settled here. It is required before multi-branch rollout and is
  tracked separately.

---

## 5. Compliance & Enforcement

- Every state-changing endpoint requires an authenticated principal and an explicit permission. There
  is no environment or flag that relaxes this.
- Hiding a control in the SPA is **never** authorization; the server re-checks every request.
- A security test asserts a non-Development configuration cannot resolve the local-auth provider.
- A test asserts every declared permission maps to a registered policy, so a typo cannot silently
  produce an endpoint that authorizes nothing.
- Bearer tokens, `Authorization` headers and refresh tokens are redacted from logs; only a correlation
  id and a hashed subject are recorded.

---

## Traceability

- Identity provider, JWT validation rules, permission catalogue, role mapping, data-scope →
  [../architecture/security-threat-model.md](../architecture/security-threat-model.md) §2–§3
- Named-human requirement, segregation of duties, approval gates →
  [0006-recommendation-decision-workflow.md](0006-recommendation-decision-workflow.md)
- Idempotency of state-changing operations → [0007-idempotency-and-replay.md](0007-idempotency-and-replay.md)
- Requirement IDs `V3-AUTH-001`…`V3-AUTH-004` →
  [../implementation/v3-design-traceability.md](../implementation/v3-design-traceability.md)
