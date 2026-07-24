# BeeEye infrastructure (Bicep)

Infrastructure-as-code for deploying BeeEye into the customer's Azure tenant.
Bicep is the single, consistently-used IaC technology.

## Layout

```
infra/
├── main.bicep              # resource-group scoped composition root
├── modules/                # one module per resource group of concerns
│   ├── monitoring.bicep    # Log Analytics + Application Insights
│   ├── keyvault.bicep      # Key Vault (RBAC, purge protection)
│   ├── storage.bicep       # ADLS Gen2 + data-lake zone containers
│   ├── servicebus.bicep    # Service Bus namespace + ingestion queue
│   ├── registry.bicep      # ACR (admin creds disabled)
│   ├── postgres.bicep      # PostgreSQL Flexible Server (Entra auth)
│   ├── containerapps-env.bicep
│   └── containerapp-api.bicep  # API host + system identity + AcrPull
├── environments/           # per-environment parameter files
│   ├── dev.bicepparam ├── test.bicepparam ├── uat.bicepparam └── prod.bicepparam
├── policies/               # Azure Policy guardrails (assigned separately)
└── scripts/                # deploy.ps1 / deploy.sh (what-if by default)
```

## Deploy

```bash
az group create -n rg-beeeye-dev -l uaenorth
infra/scripts/deploy.sh rg-beeeye-dev dev            # what-if
infra/scripts/deploy.sh rg-beeeye-dev dev --apply    # deploy
```

## Networking modes

* **Standard secured** (this skeleton): public app ingress behind Entra ID;
  storage/registry public network access enabled but shared-key/admin auth disabled.
* **Private enterprise**: set `publicNetworkAccess: 'Disabled'`, add VNet
  integration + private endpoints + private DNS modules, and a fixed outbound IP
  (NAT Gateway) for Oracle Fusion allow-listing. See
  `docs/architecture/deployment-and-ip-protection.md` for the added cost note.

## Validation

```bash
az bicep build --file infra/main.bicep      # or: bicep build infra/main.bicep
az deployment group what-if -g <rg> -f infra/main.bicep -p environments/dev.bicepparam
```

> The scaffold's Bicep is authored to current API versions but is validated in CI
> (`az bicep build`) rather than in this environment. Review `what-if` output
> before any apply.
