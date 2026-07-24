# Use-Case Mapping & Calculation Catalogue

Product: **Meridian BI — Decision Intelligence** (an AI decision-intelligence layer over Oracle Fusion).
Planning month is derived as the month after the last sales actual (**May 2026**).

## Navigation (information architecture)
- **Executive** → Decision Cockpit (UC8), Executive Overview
- **Sales Intelligence** → Order Optimization (UC1), Forecast Accuracy (UC2), Configuration Insights (UC3)
- **Supply Intelligence** → Procurement Optimization (UC4), Inventory Aging & Overstock (UC5)
- **After-Sales Intelligence** → Sales ↔ Service Correlation (UC6), Spare Parts Prediction (UC7)
- **Governance** → Decision Log, Data Health, Model & Data Lineage, Settings
- **Platform** → Ask Decision Intelligence, Data Ingestion, Data Management, Methodology, Integration Blueprint

## Per use case

### UC1 — Monthly Order Optimization (`order`)
- Source: sales history (demand), inventory (stock). Synthetic: confirmed inbound (open POs), MOQ/order multiple, procurement lead time.
- Derived: recency-weighted demand, seasonal index, safety stock, recommended order (min/base/max), projected cover, status.
- Expected Oracle source: Fusion Order Management + Inventory Management (+ Procurement for inbound).

### UC3 — Configuration Demand Insights (`config`)
- Source: sales history (model+variant+colour), inventory (cover/value). No synthetic data.
- Derived: momentum, demand-decay score, cluster (Star/Emerging/Core/Promotion/Declining/Overstock/Dead-Stock/Insufficient), discount association.
- Expected Oracle source: Fusion Order Management + Product Management.

### UC4 — Procurement Quantity Optimization (`procurement`)
- Source: sales (demand), inventory (stock, observed lead-time, purchase price, holding). Synthetic: supplier & PO performance.
- Derived: procurement range (min/base/max), achieved service level, working-capital & holding impact, supplier risk.
- Expected Oracle source: Fusion Procurement (+ Inventory, Order Management).

### UC6 — Sales ↔ After-Sales Correlation (`correlation`)
- Source: sales history (cohort sizing / installed base). Synthetic: all service events, mileage, warranty, labour, capacity.
- Derived: service-intensity index, cohort→service matrix, mileage & time-since bands, capacity forecast.
- Expected Oracle source: Fusion Service (+ CRM for mileage/warranty).

### UC7 — Spare Parts Demand Prediction (`parts`)
- Source: sales mix (drivers). Synthetic: all parts usage, stock, service events.
- Derived: parts demand forecast, reorder/safety recommendations, location balancing, emergency risk, driver waterfall, model→family intensity.
- Expected Oracle source: Fusion Inventory Management + Service.

### UC8 — Executive Decision Cockpit (`cockpit`)
- Consolidates the highest-priority output of every module into a ranked decision set.
- Derived: executive priority score, financial impact roll-up, cross-functional exceptions.

## Calculation catalogue (prototype methodology — illustrative)

- **Recency-weighted demand** = momentumWeight × recent-3-month avg + (1 − momentumWeight) × trailing velocity, × seasonal index × (1 + growth).
- **Safety stock** = z(serviceLevel) × σ(monthly demand) × √(planning + lead horizon).
- **Recommended order** = max(0, forecast demand over planning + lead horizon + safety − current stock − confirmed inbound), rounded to order multiple. Low/base/high from the demand confidence interval.
- **Procurement range** = same structure aggregated to model+variant, using synthetic supplier lead time (median ± σ) and service level.
- **Demand-decay score** (0–100, higher = worse) = recent-vs-prior momentum + zero-sales months + cover-above-target + narrow regional breadth + discount dependence.
- **Service-intensity index** = model events-per-100 normalised to the peak model (0–100). High intensity reflects installed base, usage and intervals — not necessarily quality.
- **Parts-demand forecast** = service events × per-event usage rate, split by location share; reorder point = safety stock + lead-time demand.
- **Executive priority score** = normalise(impact × urgency × confidence × controllability) → 0–100; factor breakdown shown in the explainability drawer.
- **Confidence** = derived from data volume, demand basis (location→national→model fallback), volatility and whether synthetic/missing fields are involved (High / Medium / Low).

All figures are decision-support estimates requiring business review; none write back to Oracle.
