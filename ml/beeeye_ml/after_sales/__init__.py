"""UC6 — sales vs after-sales demand correlation."""

from __future__ import annotations

from collections.abc import Sequence
from math import sqrt


def service_intensity_index(service_events: int, vehicles_in_operation: int) -> float | None:
    """Model-level service-intensity = service events per vehicle in operation.

    Returns ``None`` when there are no vehicles in operation, so the caller shows
    a data-coverage warning rather than a fabricated ratio.
    """
    if vehicles_in_operation < 0 or service_events < 0:
        raise ValueError("counts must be non-negative")
    if vehicles_in_operation == 0:
        return None
    return service_events / vehicles_in_operation


def data_coverage(records_present: int, records_expected: int) -> float:
    """Fraction of expected records actually present (0..1)."""
    if records_expected <= 0:
        return 0.0
    return max(0.0, min(1.0, records_present / records_expected))


def normalised_service_intensity(
    events_by_model: dict[str, int], vehicles_in_operation_by_model: dict[str, int]
) -> dict[str, float | None]:
    """Per-model Service-Intensity Index normalised by the fleet-wide ratio (fleet mean = 1.0).

    A model with no vehicles-in-operation maps to ``None`` (never a divide-by-zero); mirrors the
    production ``BeeEye.Analytics.AfterSales.ServiceIntensity`` normalisation.
    """
    fleet_events = 0
    fleet_vio = 0
    for model, vio in vehicles_in_operation_by_model.items():
        if vio > 0:
            fleet_vio += vio
            fleet_events += events_by_model.get(model, 0)
    fleet_ratio = fleet_events / fleet_vio if fleet_vio > 0 else None

    result: dict[str, float | None] = {}
    for model in set(events_by_model) | set(vehicles_in_operation_by_model):
        vio = vehicles_in_operation_by_model.get(model, 0)
        if vio <= 0 or fleet_ratio is None or fleet_ratio == 0:
            result[model] = None
        else:
            result[model] = (events_by_model.get(model, 0) / vio) / fleet_ratio
    return result


def pearson_correlation(x: Sequence[float], y: Sequence[float]) -> float | None:
    """Pearson correlation of two equal-length series; ``None`` when undefined (association, not causation)."""
    n = min(len(x), len(y))
    if n < 2:
        return None
    mx = _mean(x[:n])
    my = _mean(y[:n])
    sxy = sum((x[i] - mx) * (y[i] - my) for i in range(n))
    sxx = sum((x[i] - mx) ** 2 for i in range(n))
    syy = sum((y[i] - my) ** 2 for i in range(n))
    if sxx <= 0 or syy <= 0:
        return None
    return max(-1.0, min(1.0, sxy / sqrt(sxx * syy)))


def _mean(values: Sequence[float]) -> float:
    return sum(values) / len(values) if values else 0.0


__all__ = [
    "service_intensity_index",
    "data_coverage",
    "normalised_service_intensity",
    "pearson_correlation",
]
