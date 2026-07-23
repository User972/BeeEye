# Meridian BI — AI-Enabled Business Intelligence POC

An interactive, executive-grade Business Intelligence prototype for an automotive
distributor, built around two connected use cases:

1. **Inventory Aging & Overstock Risk** — explainable risk scoring, demand alignment and recommended actions.
2. **AI-Powered Sales Forecasting with Historical Back-Testing** — model vs. baseline accuracy, forecast bias and future demand.

Every KPI, chart, forecast and risk score is **calculated at runtime from the supplied
Excel workbooks** — nothing is hard-coded.

---

## What is in this repository

| Path | Purpose |
|------|---------|
| `Meridian BI.dc.html` | The full single-file application (10 screens). Open directly in a browser. |
| `engine.js` | Framework-free analytics engine: metrics, forecasting (Holt-Winters + baselines), back-testing, risk scoring, demand-velocity fallback, recommendation engine and the deterministic AI insight engine. |
| `data/dataset.js` | The two workbooks parsed to JSON and embedded (`window.BIDATA`). |
| `data/sales.json`, `data/inventory.json` | The same parsed data as plain JSON. |
| `uploads/*.xlsx` | The original supplied workbooks. |
| `docs/` | Data dictionary, derived-metric definitions, methodology, integration and assumptions/limitations. |
| `.env.example` | Configuration template for a production backend (AI provider, data paths, thresholds). |

## Screen inventory

Executive Cockpit · Inventory Intelligence · Sales Forecasting · AI Business Analyst ·
Management Actions · Reports & Exports · Data Management · Methodology & Assumptions ·
Integration Blueprint · POC Settings.

## Running the prototype

The prototype is a static web app. Any static server works:

```bash
# from the project root
python3 -m http.server 8080
# then open http://localhost:8080/Meridian%20BI.dc.html
```

No build step, package install or API key is required for the deterministic experience.

## AI Business Analyst — two modes

The AI analyst is grounded: it only answers from metrics computed by `engine.js` and never
fabricates figures.

- **Deterministic "POC Insight Engine"** (default, always available): rule- and template-based
  answers computed from the data. Every one of the required sample questions works offline.
- **Live AI** (optional): when a chat completion endpoint is available the app sends a compact,
  pre-aggregated data context plus the deterministic draft and asks the model to refine wording
  while preserving the numbers. A strict system prompt forbids inventing metrics, claiming
  causation, or implying live Oracle Fusion data. On any failure it falls back to the deterministic engine.

See `docs/METHODOLOGY.md` for the grounding approach and `.env.example` for provider config.

## Mapping the POC to a production stack

The POC computes everything client-side for a self-contained demo. `engine.js` is written as a
pure, framework-free module so its functions port directly to a backend service (Python/FastAPI or
Node) with no UI coupling. The recommended production topology is in
`docs/INTEGRATION_AZURE_ORACLE.md` and on the in-app **Integration Blueprint** screen.

```
Oracle Fusion & enterprise sources
      -> Oracle Fusion REST APIs / BICC / approved extracts
      -> Secure ingestion & scheduling (Azure Data Factory / Functions)
      -> Azure data storage & curated model (Data Lake + Azure SQL/PostgreSQL)
      -> Forecasting, risk-scoring & analytics services (containerised)
      -> AI insight & natural-language layer (Azure OpenAI, grounded)
      -> Executive dashboards & self-service analytics (this experience)
```

## Deployment

Static hosting for the prototype: Azure App Service, Azure Static Web Apps, Azure Container Apps,
or any container serving the folder. Example Dockerfile:

```dockerfile
FROM nginx:alpine
COPY . /usr/share/nginx/html
```

A production build would additionally deploy the ingestion, analytics and AI services described in
the integration doc, with secrets in Azure Key Vault and auth via Microsoft Entra ID.

## Data validation (computed, not asserted)

The app reproduces the expected dataset profile from the files themselves:
3,120 sales rows · 24,130 units · ~SAR 3.46B revenue · Jan 2022–Apr 2026 (52 months) ·
291 inventory units (unique stock_id & chassis) · ~SAR 46.75M purchase value ·
~SAR 3,235/day aggregate holding cost. Revenue and lead-time reconcile on every row.

## Known limitations

This is a POC on sample data. It does not connect live to Oracle Fusion, does not write actions
back to enterprise systems, and its risk model is configurable but not production-validated. See
`docs/ASSUMPTIONS_LIMITATIONS.md`.


---

## Decision Intelligence platform extension (all 8 use cases)

This prototype has been extended from the original 2 use cases into a single, cohesive
**AI Decision Intelligence layer for Oracle Fusion** covering all eight use cases:

| UC | Module | Route |
|----|--------|-------|
| 1 | Monthly Order Optimization | Sales Intelligence → Order Optimization |
| 2 | Sales Forecast Accuracy | Sales Intelligence → Forecast Accuracy |
| 3 | Configuration-Level Demand Insights | Sales Intelligence → Configuration Insights |
| 4 | Procurement Quantity Optimization | Supply Intelligence → Procurement Optimization |
| 5 | Inventory Aging & Overstock Risk | Supply Intelligence → Inventory Aging & Overstock |
| 6 | Sales vs After-Sales Correlation | After-Sales Intelligence → Sales ↔ Service Correlation |
| 7 | Spare Parts Demand Prediction | After-Sales Intelligence → Spare Parts Prediction |
| 8 | Executive Decision Cockpit | Executive → Decision Cockpit (landing page) |

Plus **Governance**: Decision Log, Data Health, AI Model & Data Lineage, Settings.

### New files
- `engine2.js` — deterministic synthetic fixtures (suppliers, procurement, service, parts) and the
  calculation services for UC1/3/4/6/7/8, data health and lineage. Layers on top of `engine.js`.
- `docs/DEMO_DATA_CATALOGUE.md` — supplied vs synthetic data, seed, fixtures, relationships, limitations.
- `docs/USE_CASE_MAPPING.md` — per-use-case data mapping and the full calculation catalogue.

### Shared platform patterns
- Grouped navigation (Executive / Sales / Supply / After-Sales / Governance / Platform) with subtle phase labels.
- A reusable **"Why this recommendation?" explainability drawer** (recommendation, expected impact,
  confidence + reasons, ranked drivers, historical evidence, assumptions, data lineage, model info, feedback).
- Consistent AI output labels: Observed · Calculated · Forecast · Recommendation · Simulation · Demo Data · Low Confidence.
- Persistent demo-data disclosure banners on the after-sales, parts and procurement (supplier) modules.
- A shared Decision Log — accepting a recommendation anywhere routes to a governed audit trail (no Oracle write-back).

**Oracle Fusion remains the system of record.** This layer prepares proposals and decision records; it does
not place orders, purchase orders, transfers or pricing changes. All synthetic supplier / service / parts
data is clearly labelled and is never presented as real client data.
