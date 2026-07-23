# Architectural tech debt & pending work

A living register of **intentional deviations** in the current scaffold from the target
architecture (the ADRs and the rest of `docs/architecture/`), plus the trigger that should
prompt each one. Record items here so they aren't lost in commit history.

---

## TD-1 — Data access: per-module `DbContext` + analytical read-store seam

**Status:** pending — deferred on purpose; appropriate at the current scaffold / POC scale.

### Current scaffold state
- A single shared `BeeEye.Persistence.BeeEyeDbContext` holds **every** entity
  (`SalesFact`, `InventoryItem`, `IngestionBatch`).
- The read services (`ForecastingReadService`, `InventoryReadService`) query it directly
  via EF Core (`AsNoTracking` + projections). There is no repository/read-store seam.

### Target
- **ADR-0003 §2** — EF Core for transactional writes; **Dapper for analytical reads, behind a
  repository seam**, run against the read replica.
- **module-boundaries AT-6** — exactly **one `DbContext` per module**, database-per-context,
  no cross-context reach-through.

### Action when triggered
1. Split the shared `BeeEyeDbContext` into **per-module `DbContext`s**, each owning its own
   PostgreSQL schema.
   *Trigger:* when module isolation / physical schema separation is enforced.
2. Introduce **per-module read-store interfaces** (e.g. `IInventoryReadStore`,
   `IForecastReadStore`) — EF-backed now, Dapper-backed later — so the analytics layer depends
   on the interface, not the `DbContext`.
   *Trigger:* when a hot dashboard / aging / forecast-grid query's EF-generated plan over the
   partitioned facts becomes a **measured** bottleneck.

### Explicitly NOT doing
- A generic `IRepository<T>` over EF Core — redundant with `DbContext` (Unit of Work) and
  `DbSet<T>` (repository), and it fights the projection-heavy analytics. The current
  read-service (query-service) pattern is the correct abstraction until the seam above is due.

---

## TD-2 — PostgreSQL access: password auth instead of Entra managed identity

**Status:** pending — deliberate deviation so the deployed platform can connect at all.

### Current scaffold state
- `infra/modules/postgres.bicep` enables **password auth** (Entra auth is also on), and the
  pipeline-generated admin password is threaded as a `@secure()` parameter into a **Container
  Apps secret** holding the full Npgsql connection string (`ConnectionStrings__Postgres`).
- This contradicts the target posture in **ADR-0003 §Auth** ("Entra ID via managed identity —
  no passwords") and `deployment-and-ip-protection.md` ("Passwordless by default"; secrets
  resolved from Key Vault). Those documents describe the target, not the current state.

### Target
- API authenticates to PostgreSQL with its **system-assigned managed identity** (Entra token
  auth): create a `flexibleServers/administrators` (or AAD principal) mapping for the app
  identity, add a token-refreshing Npgsql periodic-password callback, disable password auth.
- Any residual secrets move to **Key Vault references**, not inline Container Apps secrets.

### Action when triggered
*Trigger:* before the first deployment that handles customer data (secured mode hardening).

---

## TD-3 — Recommendation parameters are prose, not structured values

**Status:** pending — introduced knowingly in S6, and bounded by an explicit fallback.

### Current state
- `Recommendation.Action` stores the engine's advice as the prose the approver was shown, e.g.
  *"Prepare a transfer recommendation: 3 unit(s) Riyadh → Jeddah."* The numbers inside it exist only
  as text.
- ADR-0006 §2.3 requires an *accepted-with-modification* delta's `from` to be the value the engine
  actually recommended — otherwise a client working from a stale copy of the record can silently
  re-anchor the modification to a number the engine never produced.
- `DecisionsAndOutcomes/Application/RecommendedValues` therefore recovers that number by matching a
  quantity or a percentage in the action text (invariant culture, conservative patterns). Where it
  cannot, the stale-value check is **skipped rather than guessed at**: refusing a modification we
  cannot verify would block legitimate work to satisfy a check we are not able to perform.

### Target
- The generation path persists the engine's parameters as structured values beside the prose — for
  example a `ParametersJson` column holding `{"transfer_qty": 3}` — so the check is exact for every
  rule rather than best-effort for the rules whose wording happens to state a number.

### Action when triggered
*Trigger:* when a rule ships whose modifiable value is not stated in its action text, or when the
first modification is wrongly accepted or refused because the text could not be read.

The change belongs with generation in the `Recommendations` module (which owns what a recommendation
means), not with the decision workflow, and is additive: `RecommendedValues` becomes a fallback for
records written before the column existed.

---

## TD-4 — `AuditEvent` is not yet implemented

**Status:** pending — the trail exists, but in a narrower form than the canonical model specifies.

### Current state
- `RecommendationStatusEvent` is the append-only trail for the decision workflow: every transition
  appends a row naming the actor, the reason and the time, and nothing is ever updated or deleted.
- The canonical model's Cluster 9 `AuditEvent` (V3-API-004) — an append-only record of **every**
  consequential action with before/after hashes, across all contexts — does not exist. Actions
  outside the recommendation lifecycle (a settings change, a data-quality override) leave no trail.

### Target
- `AuditEvent` in the `Audit` context, written by a cross-cutting seam rather than per-module code.

### Action when triggered
*Trigger:* when the first consequential action outside the recommendation lifecycle ships, or when
internal audit asks for a single trail spanning contexts.
