"""UC7 — intermittent spare-parts demand (Croston / SBA / TSB).

Zero-demand periods are real signal and must NOT be treated as missing records: build
the monthly series as a dense grid with explicit zeros. These pure-Python seeds mirror
the production ``BeeEye.Analytics.SpareParts`` engine so the two stay in parity.
"""

from __future__ import annotations

from collections.abc import Sequence
from math import sqrt


def _croston_state(demand: Sequence[float], alpha: float) -> tuple[float | None, float | None, int]:
    level: float | None = None
    interval: float | None = None
    since_last = 0
    non_zero = 0
    for value in demand:
        since_last += 1
        if value > 0:
            non_zero += 1
            if level is None or interval is None:
                level, interval = float(value), float(since_last)
            else:
                level += alpha * (value - level)
                interval += alpha * (since_last - interval)
            since_last = 0
    return level, interval, non_zero


def croston(demand: Sequence[float], alpha: float = 0.1) -> float:
    """Croston's method estimate of per-period demand for intermittent series.

    Decomposes the series into demand size and inter-demand interval, smooths each with
    exponential smoothing, and divides. The baseline for intermittent demand.
    """
    if not 0 < alpha <= 1:
        raise ValueError("alpha must be in (0, 1]")
    if not demand:
        raise ValueError("demand must be non-empty")

    level, interval, _ = _croston_state(demand, alpha)
    if level is None or interval is None or interval == 0:
        return 0.0
    return level / interval


def sba(demand: Sequence[float], alpha: float = 0.1) -> float:
    """Syntetos–Boylan Approximation: Croston with the (1 − α/2) bias correction.

    Croston is known to over-forecast intermittent series; SBA is the recommended default.
    """
    return (1 - alpha / 2) * croston(demand, alpha)


def tsb(demand: Sequence[float], alpha_probability: float = 0.1, alpha_size: float = 0.1) -> float:
    """Teunter–Sani–Babai: smooth the demand *probability* every period (so it decays on
    long zero runs) times the smoothed demand size. Handles obsolescence/supersession.
    """
    if not demand:
        raise ValueError("demand must be non-empty")
    non_zero = [v for v in demand if v > 0]
    if not non_zero:
        return 0.0

    probability = len(non_zero) / len(demand)
    size = sum(non_zero) / len(non_zero)
    for value in demand:
        if value > 0:
            probability += alpha_probability * (1 - probability)
            size += alpha_size * (value - size)
        else:
            probability += alpha_probability * (0 - probability)
    return probability * size


def adi(demand: Sequence[float]) -> float | None:
    """Average inter-Demand Interval — periods per non-zero demand. ``None`` when no demand."""
    if not demand:
        return None
    k = sum(1 for v in demand if v > 0)
    return None if k == 0 else len(demand) / k


def squared_cv(demand: Sequence[float]) -> float:
    """Squared coefficient of variation of the non-zero demand sizes (population std)."""
    non_zero = [v for v in demand if v > 0]
    if len(non_zero) < 2:
        return 0.0
    mean = sum(non_zero) / len(non_zero)
    if mean == 0:
        return 0.0
    var = sum((v - mean) ** 2 for v in non_zero) / len(non_zero)
    cv = sqrt(var) / mean
    return cv * cv


def classify(demand: Sequence[float], adi_threshold: float = 1.32, cv2_threshold: float = 0.49) -> str:
    """Syntetos–Boylan–Croston class: smooth / erratic / intermittent / lumpy."""
    a = adi(demand)
    if a is None:
        return "insufficient"
    infrequent = a >= adi_threshold
    variable = squared_cv(demand) >= cv2_threshold
    if not infrequent and not variable:
        return "smooth"
    if not infrequent and variable:
        return "erratic"
    if infrequent and not variable:
        return "intermittent"
    return "lumpy"


__all__ = ["croston", "sba", "tsb", "adi", "squared_cv", "classify"]
