"""Tests for forecast-accuracy metrics, including the zero-actual edge case."""

from beeeye_ml.common import bias, mae, mape, rmse, wmape


def test_mae_and_rmse() -> None:
    actuals = [10.0, 20.0, 30.0]
    forecasts = [12.0, 18.0, 33.0]
    assert mae(actuals, forecasts) == (2 + 2 + 3) / 3
    assert round(rmse(actuals, forecasts), 4) == round((((2**2) + (2**2) + (3**2)) / 3) ** 0.5, 4)


def test_wmape_matches_definition() -> None:
    actuals = [10.0, 20.0, 30.0]
    forecasts = [12.0, 18.0, 33.0]
    assert wmape(actuals, forecasts) == (2 + 2 + 3) / 60


def test_wmape_returns_none_on_zero_actuals() -> None:
    # Must not divide by zero — the caller surfaces "insufficient demand".
    assert wmape([0.0, 0.0], [1.0, 2.0]) is None


def test_bias_sign() -> None:
    # Over-forecasting => positive bias.
    assert bias([10.0], [12.0]) == 0.2
    assert bias([10.0], [8.0]) == -0.2


def test_mape_skips_zero_actuals() -> None:
    assert mape([0.0, 10.0], [5.0, 11.0]) == 0.1
    assert mape([0.0, 0.0], [1.0, 1.0]) is None


if __name__ == "__main__":  # allows `python tests/test_metrics.py` without pytest
    import sys

    fns = [v for k, v in sorted(globals().items()) if k.startswith("test_")]
    for fn in fns:
        fn()
    print(f"OK: {len(fns)} metric tests passed")
    sys.exit(0)
