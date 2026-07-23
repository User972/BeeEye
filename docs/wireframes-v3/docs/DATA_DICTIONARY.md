# Data Dictionary

Currency is SAR throughout. Dates originate as Excel serials and are normalised to ISO (YYYY-MM-DD).

## Sales history — `sales_history_final_prepared.xlsx` (3,120 rows)

| Column | Type | Notes |
|--------|------|-------|
| `sale_date` | date | Monthly reporting period (first of month), Jan 2022 – Apr 2026 (52 months). |
| `year`, `month` | int | Redundant with `sale_date`. |
| `location` | text | 15 values incl. Mecca (which holds no inventory). |
| `model` | text | Patrol, Corolla, Haval H9, Camry, ES 350. |
| `variant` | text | VX, ZX, MX. |
| `units_sold` | int | Sales volume. |
| `unit_price` | number | List price per unit before discount. |
| `revenue` | number | Net recorded revenue. Reconciles to units x price x (1 - discount%/100) on every row. |
| `currency` | text | SAR. |
| `colour` | text | 5 values. |
| `date_of_manufacture` | date | Manufacturing date on the sales record. |
| `brand` | text | Nissan, Toyota, HAVAL, Lexus. |
| `type` | text | SUV, Hatchback, Sedan, Luxury Sedan. |
| `interior` | text | 4 values. |
| `discount_applied` | text | "Yes"/"No" (normalised to boolean). |
| `discount_pct` | int | Observed values: 0, 5, 10, 15, 20. |
| `is_ramadan` | text | "True"/"False" (normalised to boolean). |

## Inventory stock — `inventory_stock_final_prepared.xlsx` (291 rows)

| Column | Type | Notes |
|--------|------|-------|
| `stock_id` | text | Primary key — unique. |
| `chassis_no` | text | Unique vehicle identifier. |
| `model`, `variant`, `colour`, `interior`, `brand`, `type` | text | Match the sales taxonomy. |
| `location` | text | 14 values (no Mecca). |
| `date_of_purchase` | date | Start of inventory holding age. Feb–May 2026. |
| `date_of_manufacture` | date | Start of manufacturing age. Sep 2024 – Dec 2025. |
| `service_date` | date | **Meaning unconfirmed** — displayed but excluded from risk scoring. |
| `lead_time_days` | int | = date_of_purchase - date_of_manufacture (reconciles on every row). |
| `purchase_price` | number | Capital value. Total ~SAR 46.75M. |
| `holding_cost_per_day` | number | Daily carrying cost. Aggregate ~SAR 3,235/day. |
| `currency` | text | SAR. |

## Relationship

Sales and inventory join on **location + model + variant** (with a documented fallback hierarchy
where location-level history is sparse). Mecca appears in sales but not inventory and is handled
gracefully.
