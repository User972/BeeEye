# Demo Data Catalogue

The supplied workbooks (`sales_history_final_prepared.xlsx`, `inventory_stock_final_prepared.xlsx`)
do not contain enough information to fully support Use Cases 4, 6 and 7. For prototype purposes,
`engine2.js` generates **deterministic, seeded synthetic demo datasets** that are logically
correlated with the real sales and inventory data. They are **never presented as real client data**
and are labelled *Demo Data* / *Illustrative* / *Synthetic* throughout the UI.

## Determinism
- Fixed seed: `SEED = 20260531` (mulberry32 PRNG, keys hashed with FNV-1a).
- Every entity draws from a stream seeded by its own key (e.g. `po|Camry|ZX`), so results are
  identical on every load and consistent across every screen.
- Fixtures are memoised and computed once per session.

## Supplied vs generated

| Domain | Source | Notes |
|--------|--------|-------|
| Sales history | **Supplied** | 3,120 rows, Jan 2022 – Apr 2026 |
| Inventory on-hand | **Supplied** | 291 vehicle-level units |
| Suppliers | Generated | 5 pseudonymous suppliers (Supplier A/B, Regional OEM Hub, Central Import Partner, Gulf Logistics Alliance) |
| Procurement / PO history | Generated | ~195 POs over trailing 18 months; promised vs actual delivery, MOQ, order multiple, expedite/cancel flags, delay reasons; ~23 open (inbound) POs |
| Service events | Generated | Aggregated per model/age-bucket/mileage cohort; per-model intensity profiles |
| Parts master / usage | Generated | 8 synthetic SKUs (`DEMO-OIL-001`, `DEMO-BRK-001`, …) across families |
| Parts inventory | Generated | Per part × location: on-hand, reserved, inbound, reorder point, safety stock, emergency-order count |

## Fixture fields
- **Procurement**: po_id, supplier_id, supplier_name, supplier_region, model, variant, brand,
  destination, order_date, promised, actual, qty_ordered, qty_received, unit_cost, moq,
  order_multiple, lead_days, delay_days, expedited, cancelled, delay_reason, status, open, eta_days.
- **Service (aggregated)**: model, installed base, service events, events per 100, time-to-first-service,
  repeat rate, warranty rate, avg labour hours, intensity index; cohort matrix (sale quarter × months-since-sale);
  mileage bands; time-since-sale bands; workshop capacity per location.
- **Parts**: part_number, name, family, model compatibility, unit_cost, supplier_lead_time,
  per-event usage rate; per-location forecast, safety, reorder, on_hand, reserved, available,
  inbound, cover, emergency_count.

## Relationships (how synthetic ties to real data)
- Procurement quantities scale to **real national monthly demand** per model/variant; unit_cost is the
  **average real purchase price** for that configuration.
- Service cohorts are sized from the **real sales history** (units sold per quarter per model).
- Parts demand is derived from **service events × usage rate**, which in turn derive from the real sales mix.
- Supplier assignment follows the real **brand** of each model.

## Known data-quality condition (surfaced, not hidden)
- Sales data includes **Mecca**, but the inventory workbook has **no Mecca snapshot**. This is surfaced on
  the **Data Health** screen (status *Ready with assumptions*) and cover for Mecca falls back to national demand.

## Limitations
- No live Oracle Fusion connection; no write-back.
- No real supplier master, purchase-order history, service-event history, mileage, warranty or parts history.
- Prototype calculations are illustrative and not production-validated.
- `service_date` in the inventory workbook is a single date per unit and is **not** treated as a service history.
