"""Transparent forecast baselines, mirroring the wireframe engine.

These are deliberately simple and deterministic. A candidate ML model only earns
its place by beating the best of these on time-based back-testing.
"""

from __future__ import annotations

from collections.abc import Sequence


def naive(series: Sequence[float]) -> float:
    """Previous-period forecast: repeat the last observed value."""
    if not series:
        raise ValueError("series must be non-empty")
    return float(series[-1])


def moving_average(series: Sequence[float], window: int = 3) -> float:
    """Trailing moving average over the last ``window`` observations."""
    if window < 1:
        raise ValueError("window must be >= 1")
    if not series:
        raise ValueError("series must be non-empty")
    tail = series[-window:]
    return sum(tail) / len(tail)


def seasonal_naive(series: Sequence[float], period: int = 12) -> float:
    """Seasonal-naive: value from the same period last cycle (e.g. last year).

    Falls back to :func:`naive` when there is not yet a full season of history —
    a documented cold-start behaviour for new models/configurations.
    """
    if period < 1:
        raise ValueError("period must be >= 1")
    if not series:
        raise ValueError("series must be non-empty")
    if len(series) < period:
        return naive(series)
    return float(series[-period])
