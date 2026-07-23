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
