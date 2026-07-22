"""UC4 — procurement quantity + safety stock."""

from __future__ import annotations

from math import sqrt

# Service-level -> z (one-sided normal) for common targets.
_Z_FOR_SERVICE_LEVEL: dict[float, float] = {
    0.90: 1.2816,
    0.95: 1.6449,
    0.975: 1.9600,
    0.99: 2.3263,
}


def safety_stock(demand_std_per_period: float, lead_time_periods: float, service_level: float = 0.95) -> float:
    """Safety stock = z * sigma_demand * sqrt(lead_time).

    ``service_level`` is looked up to the nearest supported target. Recommendations
    must be expressed as ranges downstream, never as false-precision point values,
    and must account for inbound inventory before an order quantity is derived.
    """
    if demand_std_per_period < 0 or lead_time_periods < 0:
        raise ValueError("demand_std_per_period and lead_time_periods must be non-negative")
    z = _Z_FOR_SERVICE_LEVEL.get(service_level)
    if z is None:
        z = min(_Z_FOR_SERVICE_LEVEL.items(), key=lambda kv: abs(kv[0] - service_level))[1]
    return z * demand_std_per_period * sqrt(lead_time_periods)


__all__ = ["safety_stock"]
