"""UC2 — forecasting baselines and accuracy.

Every candidate model must beat a transparent baseline (see
``docs/architecture/mlops-and-models.md``). These are the baselines.
"""

from .baselines import moving_average, naive, seasonal_naive

__all__ = ["naive", "moving_average", "seasonal_naive"]
