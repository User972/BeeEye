"""Explainable additive inventory aging-risk score (UC5).

A transparent 0–100 score shown as an additive breakdown — never a black box —
mirroring the wireframe "POC Explainable Risk Model" (see
``docs/wireframes/docs/METHODOLOGY.md``). Default weights:

    stock-cover 30% · holding age 25% · declining demand 20% · holding-cost 15% · lead-time 10%

Bands: Low 0–34 · Medium 35–59 · High 60–79 · Critical 80–100.

The production model is validated separately; this seed encodes the deterministic
baseline the ML model must improve upon.
"""

from __future__ import annotations

from dataclasses import dataclass

DEFAULT_WEIGHTS: dict[str, float] = {
    "stock_cover": 30.0,
    "holding_age": 25.0,
    "declining_demand": 20.0,
    "holding_cost": 15.0,
    "lead_time": 10.0,
}

_BANDS = (("Low", 0, 34), ("Medium", 35, 59), ("High", 60, 79), ("Critical", 80, 100))


@dataclass(frozen=True)
class RiskFactors:
    """Each factor is a normalised 0..1 severity (1 = worst)."""

    stock_cover: float
    holding_age: float
    declining_demand: float
    holding_cost: float
    lead_time: float


@dataclass(frozen=True)
class RiskResult:
    score: float
    band: str
    contributions: dict[str, float]


def risk_band(score: float) -> str:
    for name, low, high in _BANDS:
        if low <= score <= high:
            return name
    return "Critical" if score > 100 else "Low"


def _clamp01(value: float) -> float:
    return max(0.0, min(1.0, value))


def score_risk(factors: RiskFactors, weights: dict[str, float] | None = None) -> RiskResult:
    """Weighted-sum risk score with a per-factor contribution breakdown."""
    w = weights or DEFAULT_WEIGHTS
    values = {
        "stock_cover": _clamp01(factors.stock_cover),
        "holding_age": _clamp01(factors.holding_age),
        "declining_demand": _clamp01(factors.declining_demand),
        "holding_cost": _clamp01(factors.holding_cost),
        "lead_time": _clamp01(factors.lead_time),
    }
    contributions = {k: round(values[k] * w[k], 2) for k in values}
    score = round(sum(contributions.values()), 2)
    return RiskResult(score=score, band=risk_band(score), contributions=contributions)
