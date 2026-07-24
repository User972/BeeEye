"""Forecast-accuracy metrics.

Pure-Python implementations mirroring the definitions in
``docs/wireframes/docs/DERIVED_METRICS.md``. WMAPE is the primary metric because
it is robust to zero-demand periods (a documented forecasting edge case); MAPE is
only defined where actuals are non-zero.
"""

from __future__ import annotations

from collections.abc import Sequence
from math import sqrt

Number = float


def _validate(actuals: Sequence[Number], forecasts: Sequence[Number]) -> None:
    if len(actuals) != len(forecasts):
        raise ValueError(
            f"actuals and forecasts must be the same length ({len(actuals)} vs {len(forecasts)})"
        )
    if not actuals:
        raise ValueError("at least one observation is required")


def mae(actuals: Sequence[Number], forecasts: Sequence[Number]) -> float:
    """Mean absolute error."""
    _validate(actuals, forecasts)
    return sum(abs(a - f) for a, f in zip(actuals, forecasts)) / len(actuals)


def rmse(actuals: Sequence[Number], forecasts: Sequence[Number]) -> float:
    """Root mean squared error."""
    _validate(actuals, forecasts)
    return sqrt(sum((a - f) ** 2 for a, f in zip(actuals, forecasts)) / len(actuals))


def wmape(actuals: Sequence[Number], forecasts: Sequence[Number]) -> float | None:
    """Weighted absolute percentage error = sum|a-f| / sum(a).

    Returns ``None`` when the actuals sum to zero, rather than dividing by zero —
    the caller must surface "insufficient demand" instead of a fabricated score.
    """
    _validate(actuals, forecasts)
    denom = sum(actuals)
    if denom == 0:
        return None
    return sum(abs(a - f) for a, f in zip(actuals, forecasts)) / denom


def bias(actuals: Sequence[Number], forecasts: Sequence[Number]) -> float | None:
    """Forecast bias = sum(f-a) / sum(a). Positive => over-forecasting."""
    _validate(actuals, forecasts)
    denom = sum(actuals)
    if denom == 0:
        return None
    return sum(f - a for a, f in zip(actuals, forecasts)) / denom


def mape(actuals: Sequence[Number], forecasts: Sequence[Number]) -> float | None:
    """Mean absolute percentage error over non-zero actuals only.

    Returns ``None`` when every actual is zero (MAPE is undefined there).
    """
    _validate(actuals, forecasts)
    pairs = [(a, f) for a, f in zip(actuals, forecasts) if a != 0]
    if not pairs:
        return None
    return sum(abs(a - f) / abs(a) for a, f in pairs) / len(pairs)
