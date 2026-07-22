"""Tests for baselines, aging risk, procurement, order optimisation and parts."""

from beeeye_ml.after_sales import (
    normalised_service_intensity,
    pearson_correlation,
    service_intensity_index,
)
from beeeye_ml.forecasting import moving_average, naive, seasonal_naive
from beeeye_ml.inventory_aging import RiskFactors, score_risk
from beeeye_ml.order_optimisation import OrderConstraints, net_requirement
from beeeye_ml.procurement import safety_stock
from beeeye_ml.spare_parts import adi, classify, croston, sba, squared_cv, tsb


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
    assert abs(rate - 5 / 3) < 1e-9
    assert croston([0.0, 0.0, 0.0]) == 0.0


def test_sba_and_tsb_match_reference_values() -> None:
    series = [0.0, 0.0, 5.0, 0.0, 0.0, 5.0]
    # SBA = (1 - alpha/2) * Croston = 0.95 * 5/3
    assert abs(sba(series, 0.1) - 0.95 * (5 / 3)) < 1e-9
    assert sba(series, 0.1) < croston(series, 0.1)
    # TSB reference value (see the C# IntermittentTests canonical example).
    assert abs(tsb(series, 0.1, 0.1) - 1.750235) < 1e-5
    assert tsb([0.0, 0.0, 0.0]) == 0.0


def test_adi_cv2_and_classification() -> None:
    assert adi([0.0, 0.0, 5.0, 0.0, 0.0, 5.0]) == 3.0
    assert adi([0.0, 0.0, 0.0]) is None
    assert squared_cv([0.0, 0.0, 5.0, 0.0, 0.0, 5.0]) == 0.0
    assert classify([10.0, 11, 9, 10, 12, 10, 11, 9]) == "smooth"
    assert classify([0.0, 0, 5, 0, 0, 5, 0, 0, 5, 0, 0, 5]) == "intermittent"
    assert classify([0.0, 0, 2, 0, 0, 0, 25, 0, 0, 10, 0, 0]) == "lumpy"


def test_service_intensity_guards_zero_fleet() -> None:
    assert service_intensity_index(100, 50) == 2.0
    assert service_intensity_index(100, 0) is None


def test_normalised_intensity_has_fleet_mean_one() -> None:
    # Fast: 200 events / 100 vehicles = 2.0; Slow: 100/100 = 1.0; fleet ratio = 300/200 = 1.5.
    idx = normalised_service_intensity({"Fast": 200, "Slow": 100}, {"Fast": 100, "Slow": 100})
    assert abs(idx["Fast"] - 2.0 / 1.5) < 1e-9
    assert abs(idx["Slow"] - 1.0 / 1.5) < 1e-9
    # Zero fleet -> None, never a divide-by-zero.
    idx0 = normalised_service_intensity({"Ghost": 10}, {"Ghost": 0})
    assert idx0["Ghost"] is None


def test_pearson_correlation_edge_cases() -> None:
    assert pearson_correlation([1, 2, 3, 4], [2, 4, 6, 8]) == 1.0
    assert pearson_correlation([1, 2, 3, 4], [8, 6, 4, 2]) == -1.0
    assert pearson_correlation([5, 5, 5], [1, 2, 3]) is None
    assert pearson_correlation([1], [2]) is None


if __name__ == "__main__":
    import sys

    fns = [v for k, v in sorted(globals().items()) if k.startswith("test_")]
    for fn in fns:
        fn()
    print(f"OK: {len(fns)} model tests passed")
    sys.exit(0)
