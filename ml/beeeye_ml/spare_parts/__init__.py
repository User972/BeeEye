"""UC7 — intermittent spare-parts demand (Croston's method).

Zero-demand periods are real signal and must NOT be treated as missing records.
"""

from __future__ import annotations

from collections.abc import Sequence


def croston(demand: Sequence[float], alpha: float = 0.1) -> float:
    """Croston's method estimate of per-period demand for intermittent series.

    Returns the estimated demand rate (level / interval). Suitable as a baseline
    for sparse parts demand; SBA/TSB variants are added for real workloads.
    """
    if not 0 < alpha <= 1:
        raise ValueError("alpha must be in (0, 1]")
    if not demand:
        raise ValueError("demand must be non-empty")

    level: float | None = None
    interval: float | None = None
    since_last = 0

    for value in demand:
        since_last += 1
        if value > 0:
            if level is None or interval is None:
                level, interval = float(value), float(since_last)
            else:
                level += alpha * (value - level)
                interval += alpha * (since_last - interval)
            since_last = 0

    if level is None or interval is None or interval == 0:
        return 0.0
    return level / interval


__all__ = ["croston"]
