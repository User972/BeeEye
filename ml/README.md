# BeeEye ML

Python ML and statistical workloads for the BeeEye platform, packaged as
containerised, reproducible batch training and scoring jobs (Azure Container Apps
Jobs; Azure ML where the customer requires a managed registry).

## Layout

| Package | Use case | Purpose |
|---------|----------|---------|
| `beeeye_ml.common` | — | Shared metrics (WMAPE, MAE, RMSE, bias, MAPE) and evaluation |
| `beeeye_ml.forecasting` | UC2 | Baselines (naive, moving average, seasonal-naive) |
| `beeeye_ml.inventory_aging` | UC5 | Explainable additive aging-risk score |
| `beeeye_ml.order_optimisation` | UC1 | Constraint-aware order net-requirement |
| `beeeye_ml.procurement` | UC4 | Safety stock for target service levels |
| `beeeye_ml.after_sales` | UC6 | Service-intensity index and data coverage |
| `beeeye_ml.spare_parts` | UC7 | Croston intermittent-demand baseline |

The seed modules are pure standard library and mirror the deterministic logic in
the wireframe `engine.js`, so they establish the **baselines every ML model must
beat** (see `docs/architecture/mlops-and-models.md`).

## Develop

```bash
python -m venv .venv && . .venv/Scripts/activate   # Windows: .venv\Scripts\activate
pip install -e ".[dev]"
pytest
ruff check .
mypy beeeye_ml
```

Without installing anything you can still run the seed tests:

```bash
# from the ml/ directory
PYTHONPATH=. python tests/test_metrics.py
PYTHONPATH=. python tests/test_models.py
```
