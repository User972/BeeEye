"""UC1 — monthly vehicle order optimisation.

The platform strictly separates: demand forecast -> business constraints ->
optimisation -> recommendation -> human decision. An unconstrained forecast must
never be presented as an order recommendation.
"""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class OrderConstraints:
    current_inventory: int
    inbound_inventory: int
    confirmed_orders: int
    min_order_quantity: int = 0
    allocation_limit: int | None = None


def net_requirement(forecast_demand: float, constraints: OrderConstraints) -> int:
    """Net order requirement after netting off existing and inbound supply.

    This is the constraint layer only — it deliberately does not forecast. The
    result is clamped to the minimum order quantity and any factory allocation
    limit, and is never negative.
    """
    available = constraints.current_inventory + constraints.inbound_inventory + constraints.confirmed_orders
    raw = max(0, round(forecast_demand) - available)
    quantity = max(raw, constraints.min_order_quantity) if raw > 0 else 0
    if constraints.allocation_limit is not None:
        quantity = min(quantity, constraints.allocation_limit)
    return quantity


__all__ = ["OrderConstraints", "net_requirement"]
