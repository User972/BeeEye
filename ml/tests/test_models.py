"""Tests for baselines, aging risk, procurement, order optimisation and parts."""

from beeeye_ml.after_sales import service_intensity_index
from beeeye_ml.forecasting import moving_average, naive, seasonal_naive
from beeeye_ml.inventory_aging import RiskFactors, score_risk
from beeeye_ml.order_optimisation import OrderConstraints, net_requirement
from beeeye_ml.procurement import safety_stock
from beeeye_ml.spare_parts import croston


def test_baselines() -> None:
    series = [10.0, 12.0, 11.0, 20.0]
    assert naive(series) == 20.0
    assert moving_average(series, 3) == (12 + 11 + 20) / 3
    # Not enough history for a full season -> falls back to naive.
    assert seasonal_naive(series, period=12) == 20.0
    long_series = list(range(1, 15))  # 14 points
    assert seasonal_naive([float(x) for x in long_series], period=12) == 3.0


def test_risk_score_breakdown_and_band() -> None:
    factors = RiskFactors(
        stock_cover=1.0, holding_age=1.0, declining_demand=1.0, holding_cost=1.0, lead_time=1.0
    )
    result = score_risk(factors)
    assert result.score == 100.0  # all weights sum to 100
    assert result.band == "Critical"
    assert sum(result.contributions.values()) == 100.0

    low = score_risk(RiskFactors(0.1, 0.1, 0.1, 0.1, 0.1))
    assert low.band == "Low"


def test_safety_stock_increases_with_service_level() -> None:
    ss90 = safety_stock(10.0, 4.0, service_level=0.90)
    ss99 = safety_stock(10.0, 4.0, service_level=0.99)
    assert ss99 > ss90 > 0


def test_net_requirement_nets_off_inbound() -> None:
    constraints = OrderConstraints(current_inventory=5, inbound_inventory=10, confirmed_orders=2)
    # Forecast of 30 against 17 available => 13, but MOQ bumps it.
    assert net_requirement(30, constraints) == 13
    # Never ignore inbound: enough supply => order nothing.
    assert net_requirement(15, constraints) == 0


def test_croston_handles_intermittent_demand() -> None:
    rate = croston([0.0, 0.0, 5.0, 0.0, 0.0, 5.0])
    assert rate > 0
    assert croston([0.0, 0.0, 0.0]) == 0.0


def test_service_intensity_guards_zero_fleet() -> None:
    assert service_intensity_index(100, 50) == 2.0
    assert service_intensity_index(100, 0) is None


if __name__ == "__main__":
    import sys

    fns = [v for k, v in sorted(globals().items()) if k.startswith("test_")]
    for fn in fns:
        fn()
    print(f"OK: {len(fns)} model tests passed")
    sys.exit(0)
