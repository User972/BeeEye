"""UC6 — sales vs after-sales demand correlation."""

from __future__ import annotations

from collections.abc import Sequence


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


def _mean(values: Sequence[float]) -> float:
    return sum(values) / len(values) if values else 0.0


__all__ = ["service_intensity_index", "data_coverage"]
