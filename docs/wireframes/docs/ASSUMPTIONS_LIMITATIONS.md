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
