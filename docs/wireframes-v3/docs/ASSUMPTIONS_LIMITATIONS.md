# POC Assumptions & Limitations

## Assumptions
- Inventory **Analysis Date** is a configurable POC assumption (default 30 June 2026); the current
  system date is never used silently. Changing it recomputes age, holding cost, aging bands, risk and recommendations.
- The customer's original historical forecasts were not supplied — accuracy is demonstrated via back-testing.
- `service_date` business meaning is unconfirmed; it is shown in detail records but excluded from risk scoring
  and flagged for business clarification.
- Sparse location-model-variant demand uses a transparent fallback hierarchy, shown per calculation;
  a missing combination is not automatically treated as zero demand.
- Recommendations are decision-support suggestions requiring business review, not automated actions.
- Risk scoring is a configurable POC model, not a production-validated model.

## Limitations
- Uses supplied sample/demo data.
- Does not connect live to Oracle Fusion; does not write actions back to enterprise systems.
- Forecasts are prototype estimates.
- Risk scoring is not yet validated against production outcomes.
- Management actions are stored only in the browser (localStorage) within the POC.
- Detailed security, data governance and full integration design would be completed during implementation.


---

## Extended platform (UC1, 3, 4, 6, 7, 8) — additional assumptions

- **Planning month** is the month after the last sales actual (Apr 2026 → **May 2026**).
- **Confirmed inbound** for order optimization comes from a synthetic open-PO fixture, labelled Demo Data.
- **Procurement lead time** used for planning is a synthetic supplier value (realistic 45–135 days), shown
  separately from the workbook's observed `lead_time_days` (manufacture→stock age), which is labelled *Observed*.
- **Target service level** defaults to 95% (a scenario assumption); MOQ / order multiples use illustrative supplier data.
- **After-sales & parts** (UC6, UC7) use synthetic service and parts fixtures; a persistent banner discloses this.
  Service intensity reflects installed base, usage and intervals — high intensity does **not** imply poor quality.
- **Workshop capacity** and **transfer suggestions** are illustrative simulations; logistics cost is not modelled.
- **Executive priority score** is a transparent rule (impact × urgency × confidence × controllability), not a trained model.
- Data mismatch (Mecca sales without an inventory snapshot) is surfaced on **Data Health**, not hidden.
- All recommendations are decision-support; nothing is written back to Oracle Fusion in this phase.
