# Derived Metric Definitions

All computed in `engine.js` from the workbooks.

## Sales
- Gross revenue = units_sold x unit_price
- Discount value = gross revenue - recorded revenue
- Average selling price = revenue / units_sold
- Monthly volume / revenue = sum over the calendar month (missing months filled as 0 at aggregated levels)
- YoY / MoM growth, YTD, rolling 3/6/12-month sums
- Breakdowns by location, brand, model, variant, type, colour, interior, and Ramadan vs non-Ramadan

## Forecast accuracy (holdout back-test)
- **WMAPE** = Σ|actual - forecast| / Σ(actual)  — primary metric (robust to zeros)
- MAE = mean(|actual - forecast|)
- RMSE = sqrt(mean((actual - forecast)^2))
- Forecast bias = Σ(forecast - actual) / Σ(actual)
- Over/under-forecast frequency = share of holdout months where forecast >/< actual
- MAPE reported where actuals are non-zero

## Inventory (as of the configurable Analysis Date, default 30 Jun 2026)
- Inventory age (days) = analysis date - date_of_purchase
- Manufacturing age (days) = analysis date - date_of_manufacture
- Accumulated holding cost = max(0, inventory age) x holding_cost_per_day
- Demand velocity = trailing-N-month average monthly units (fallback hierarchy — see METHODOLOGY)
- Months of stock cover = current group stock units / average monthly units sold
- Demand trend = recent 3-month avg vs prior 3-month avg (increasing / stable / declining)

Aggregates: inventory value, accumulated & daily holding cost, average inventory/manufacturing age
and lead time, value & units by aging band, value by risk band, high/critical-risk value, and
transfer / promotion / procurement-pause candidate counts.
