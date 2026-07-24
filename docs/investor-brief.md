# BeeEye — Investor & Stakeholder Brief

> AI decision-intelligence for **ADMC** automotive distribution. Turns Oracle Fusion ERP/CRM data
> into **explainable, auditable, governed** recommendations across eight business use cases.
> _Status as of 2026-07-24. Figures are verifiable from the repository; demonstration numbers below
> are drawn from the seeded sample dataset and labelled as such._

---

## The one-line

Distributors sit on ERP data they cannot act on with confidence. BeeEye reads that data (never
writes to it), computes the decision, **explains why**, and wraps a **governed human workflow** —
who decided, what they changed, who signed off, what resulted — around every recommendation so the
output is trusted and auditable rather than another dashboard.

## Status at a glance

| | |
|---|---|
| **Use cases live end-to-end** | **8 of 8** (UC1–UC8), on real PostgreSQL with live `/api/v1` endpoints and working React screens |
| **Governance** | Frozen, append-only recommendation records; human decision log; idempotent writes; nothing is ever edited or deleted |
| **Security** | Entra ID authentication + permission-based authorization; segregation of duties enforced and tested |
| **Explainability** | Shared drawer — every figure, forecast and recommendation answers "why?" in one consistent panel |
| **Automated tests** | **1085 green** (885 backend + 200 web) plus 15 ML tests; 0 build warnings |
| **Modules** | 19 bounded contexts (11 operational, 8 scaffolded for the next phase) |
| **Deployment** | Azure (Container Apps, Service Bus, Key Vault, Entra ID) via Bicep — deployable into the customer's own tenant |

## The eight use cases

| UC | Decision it drives | Data basis |
|----|--------------------|------------|
| **UC1** | Monthly vehicle **order optimisation** — how many of each model to order | Real sales history |
| **UC2** | **Sales forecasting** with measured accuracy (back-tested) | Real sales history |
| **UC3** | **Configuration demand** — which trims/colours are moving, which are stuck | Real sales history |
| **UC4** | **Procurement quantity** — safety stock and reorder quantities | Real sales history |
| **UC5** | **Inventory aging & overstock risk** — what to redistribute or discount | Real stock data |
| **UC6** | **Sales ↔ after-sales correlation** — where service demand is building | _Synthetic-demo¹_ |
| **UC7** | **Spare-parts demand** — intermittent-demand forecasting (Croston/SBA/TSB) | _Synthetic-demo¹_ |
| **UC8** | **Executive decision cockpit** — the top prioritised decisions across UC1–UC7, ranked by impact, with monetary exposure | Aggregates the above |

¹ UC6/UC7 run on a deterministic after-sales/parts dataset **derived from the real sales history**.
It is clearly labelled in the UI and the API and is **never presented as real Oracle Fusion data** —
an integrity choice, not a shortcut: the platform will not show an executive a fabricated figure as
measured.

## Why it is defensible

- **Governed write path, not a dashboard.** Engine recommendations are frozen at generation; the
  human layer is *appended* around the original (ADR 0006). There is **no delete path at any layer** —
  changing your mind appends a row. This is the audit trail a regulated distributor needs and that
  a BI tool structurally cannot provide.
- **Explainability is first-class.** A single drawer explains every number — its lineage, drivers,
  confidence, and honest assumptions (e.g. an assumed labour rate is stated, not hidden). Feedback on
  explanations is captured append-only.
- **Read-only against the system of record.** **BeeEye never writes to Oracle Fusion.** Oracle is the
  read-only system of record behind a versioned anti-corruption layer, so the platform can be adopted
  without risk to the customer's ERP.
- **GenAI narrates, never decides.** The AI layer explains validated, deterministic figures; it never
  computes a business value. Provider-neutral by design (ADR 0004), so no lock-in to one model vendor.
- **Deploys into the customer's tenant** with an IP-protection release model — relevant for enterprise
  procurement and data-residency requirements.

## Engineering maturity

- **One coherent architecture** — a .NET 10 modular monolith with strict, test-enforced module
  boundaries; React 19 + TypeScript (strict) front end; Python ML baselines every production model
  must beat. No parallel stack per use case.
- **Cross-context communication through published contracts only** (`IDecisionSignalProvider` for the
  cockpit, `IExplainabilityProvider` for the drawer) — enforced by architecture tests, so the
  boundaries are guaranteed, not merely intended.
- **1085 automated tests, all green, 0 build warnings.** Backend **885** = 384 analytics · 297 unit ·
  6 architecture · 198 integration (real PostgreSQL via Testcontainers); web **200**; ML **15**.
- **Decisions documented** — eight Architecture Decision Records cover the modular monolith, tech
  stack, operational database, AI abstraction, the Oracle anti-corruption layer, the recommendation
  decision workflow, idempotency, and authentication.

## What the demo shows (seeded sample data)

On the seeded sample dataset the executive cockpit surfaces a ranked, monetised decision feed — for
example a multi-model order-allocation opportunity valued in the tens of millions of SAR over the
stated planning horizon, alongside inventory-redistribution, procurement-reduction and workshop-
capacity decisions — each drilling into the owning screen and each explainable in the drawer.
Every figure is computed from the sample data; none is hard-coded.

## Honest limitations (what is *not* done)

- **UC6/UC7 use synthetic-demo after-sales/parts data** (see above) — no real service history exists
  to integrate yet.
- **Supplier / purchase-order data is not integrated**, so a supplier-delay decision (and any safety
  stock that would depend on supplier reliability) is deliberately omitted rather than fabricated.
- **Eight bounded contexts are scaffolded, not live** (Data Quality, Audit, Integration, Master Data,
  Notifications, and others) — the roadmap's next phase.
- **Live AI narration and background expiry/supersession jobs** are specified and seamed but not yet
  shipped.

## Roadmap (next)

The remaining slices (S7–S11) are the Data Health / Lineage / Settings screens, background expiry &
supersession jobs, and live AI narration — tracked slice by slice in
[docs/implementation/v3-progress.md](implementation/v3-progress.md).

## Read more

- Product overview & quick start: [../README.md](../README.md)
- Architecture: [architecture/overview.md](architecture/overview.md) · Decisions: [adr/](adr/)
- Use-case specifications: [product/use-cases/](product/use-cases/)
- Data integration & the synthetic-demo provenance: [architecture/data-integration-and-quality.md](architecture/data-integration-and-quality.md)
- IP-protection release model: [architecture/deployment-and-ip-protection.md](architecture/deployment-and-ip-protection.md)
