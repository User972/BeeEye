# Azure Policy

Governance guardrails assigned at the subscription or management-group scope
(outside the per-environment resource-group deployment). Assign these before the
first `main.bicep` deployment.

| Policy | Intent |
|--------|--------|
| Require tags (`workload`, `environment`, `managedBy`) | Cost allocation and ownership |
| Allowed locations | Keep data in approved regions (data residency) |
| Deny storage accounts with public blob access | Matches `allowBlobPublicAccess: false` |
| Deny public network access on PostgreSQL / Key Vault (prod) | Private-enterprise mode |
| Require diagnostic settings to Log Analytics | Observability |
| Deny ACR admin user | IP protection (managed-identity pulls only) |

These are intentionally kept as assignments (not baked into `main.bicep`) so the
customer's platform team owns the guardrails independently of the workload
deployment.
