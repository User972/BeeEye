"""UC5 — explainable inventory aging-risk scoring."""

from .risk import RiskFactors, RiskResult, risk_band, score_risk

__all__ = ["RiskFactors", "RiskResult", "score_risk", "risk_band"]
