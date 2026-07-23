# Integration Blueprint — Oracle Fusion & Azure (future state)

> Not connected in the POC. This is the target production architecture.

```
Oracle Fusion & Enterprise Sources (ERP, sales, inventory, finance, after-sales)
  -> Oracle Fusion REST APIs / BICC / approved extracts   (governed extraction)
  -> Secure ingestion & scheduling                        (Azure Data Factory / Functions, validation on load)
  -> Azure data storage & curated business model          (Data Lake / Blob + Azure SQL or PostgreSQL)
  -> Forecasting, risk-scoring & analytics services       (containerised Python, cached results)
  -> AI insight & natural-language layer                  (Azure OpenAI, strict grounding)
  -> Executive dashboards & self-service analytics        (this experience)
```

## Candidate Azure services (not all mandatory)
- Azure App Service / Container Apps — host the web app and analytics services
- Azure SQL / Database for PostgreSQL — curated model and metrics store
- Azure Blob Storage / Data Lake — raw extracts and processed datasets
- Azure Functions / Data Factory — scheduled Oracle Fusion ingestion
- Azure OpenAI — grounded natural-language insights
- Azure Key Vault — secrets and connection strings
- Microsoft Entra ID — authentication and role-based access
- Application Insights — monitoring and telemetry

## Cross-cutting concerns
Authentication & RBAC (Entra ID; Exec / Analyst / IT personas) · secrets in Key Vault + managed
identity (no secrets in client code) · scheduled refresh with validation gates · auditability &
data lineage (source row references preserved) · monitoring (App Insights) · **human approval
required before any recommended action is executed** · dev/test/prod separation · periodic model
re-training with back-test validation.

## Porting the POC engine
`engine.js` is framework-free and UI-agnostic. Its functions (metrics, forecasting, risk scoring,
recommendations) map directly onto a backend service; replace the embedded `window.BIDATA` with a
data-access layer reading the curated Azure model fed from Oracle Fusion.
