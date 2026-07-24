# Methodology

## Forecasting (AI-powered with historical back-testing)

The customer's original historical forecasts were not supplied, so accuracy is demonstrated by
**back-testing**: train on earlier periods, predict a known later holdout, compare to actuals.

- **Training / holdout:** default train on all-but-last-6-months, predict the final 6 (holdout is
  selectable 3/6/12). The future forecast refits on all history.
- **Methods compared:** previous-month naive, 3-month moving average, seasonal naive (same month
  last year), and Holt-Winters additive (level/trend/seasonal, period 12).
- **Selection:** the model with the lowest WMAPE is chosen; the UI shows the full comparison so the
  choice is transparent. (On the sample data, seasonal-naive is frequently competitive — the tool
  reports this honestly rather than forcing a fancier model.)
- **Confidence intervals** derive from back-test residual spread (80% default, 90/95 selectable).
- **Explanations** describe recent-vs-prior trend, seasonality and Ramadan/discount association only
  — never causation ("associated with", not "caused by").

## Inventory risk — "POC Explainable Risk Model"

A transparent 0–100 score, shown as an additive breakdown (never a black box). Default weights:

| Factor | Weight |
|--------|--------|
| Stock-cover risk | 30% |
| Inventory holding age | 25% |
| Declining demand trend | 20% |
| Holding-cost exposure | 15% |
| Lead-time risk | 10% |

Bands: Low 0–34 · Medium 35–59 · High 60–79 · Critical 80–100. Aging bands (days):
New ≤30 · Healthy ≤60 · Watch ≤90 · High attention ≤120 · Critical >120. All weights and
thresholds are editable on the **POC Settings** screen and recompute live.

### Demand fallback hierarchy (for sparse location-model-variant history)
1. Location + model + variant, where sufficient recent history exists.
2. National model + variant, scaled by the location's historical sales share.
3. Model-level national demand divided across selling locations.
4. Otherwise labelled "insufficient demand history".

The basis actually used is shown per calculation. Stock cover = current group stock units /
trailing-N-month average monthly units.

### Recommendation engine
Transparent rules produce: Retain · Transfer · Targeted promotion · Controlled discount (within the
observed 0–20% range) · Pause/reduce procurement · Prioritise liquidation · Investigate demand data.
Each recommendation carries its rationale, supporting evidence, expected outcome, confidence and
assumptions.

## AI grounding
The AI layer only uses metrics computed by the engine, states when data is unavailable or a fallback
was used, avoids causal claims and production-grade validation claims, and never implies the sample
data is live Oracle Fusion data. Live mode receives a compact aggregated context and must preserve
the engine's numbers.
