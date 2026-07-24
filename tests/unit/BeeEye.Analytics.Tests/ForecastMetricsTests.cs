using BeeEye.Analytics;
using BeeEye.Analytics.Forecasting;
using Xunit;

namespace BeeEye.Analytics.Tests;

public class ForecastMetricsTests
{
    // actual=[10,20,30], pred=[12,18,33]
    //   ae   = [2, 2, 3]         sum = 7
    //   se   = [4, 4, 9]         sum = 17
    //   diff = [2, -2, 3]        sum = 3
    //   sa   = 60
    //   over = 2 (12>10, 33>30), under = 1 (18<20)
    //   mape terms = [2/10, 2/20, 3/30] = [0.2, 0.1, 0.1]
    [Fact]
    public void Compute_SimpleExample_ProducesExactMetrics()
    {
        var actual = new double[] { 10, 20, 30 };
        var pred = new double[] { 12, 18, 33 };

        var m = ForecastMetrics.Compute(actual, pred);

        Assert.NotNull(m.Wmape);
        Assert.Equal(7.0 / 60.0 * 100.0, m.Wmape!.Value, 6); // 11.666667
        Assert.Equal(7.0 / 3.0, m.Mae, 6);                    // 2.333333
        Assert.Equal(Math.Sqrt(17.0 / 3.0), m.Rmse, 6);       // 2.380476
        Assert.NotNull(m.Bias);
        Assert.Equal(3.0 / 60.0 * 100.0, m.Bias!.Value, 6);   // 5.0
        Assert.Equal(1.0, m.BiasAbs, 6);                      // mean(diff) = 3/3
        Assert.NotNull(m.Mape);
        Assert.Equal(0.4 / 3.0 * 100.0, m.Mape!.Value, 6);    // 13.333333
        Assert.Equal(2.0 / 3.0 * 100.0, m.OverPct, 6);        // 66.666667
        Assert.Equal(1.0 / 3.0 * 100.0, m.UnderPct, 6);       // 33.333333
        Assert.Equal(3, m.N);
    }

    [Fact]
    public void Compute_ReturnsAccuracyMetricsRecord()
    {
        var m = ForecastMetrics.Compute(new double[] { 1 }, new double[] { 1 });
        Assert.IsType<AccuracyMetrics>(m);
    }

    // All-equal predictions => every error is zero.
    // f==a for all i so neither over nor under increments; sa=60 (non-zero) so
    // percentage metrics resolve to 0, not null. mape terms are all 0.
    [Fact]
    public void Compute_AllEqual_ZeroErrors()
    {
        var actual = new double[] { 10, 20, 30 };
        var pred = new double[] { 10, 20, 30 };

        var m = ForecastMetrics.Compute(actual, pred);

        Assert.NotNull(m.Wmape);
        Assert.Equal(0.0, m.Wmape!.Value, 6);
        Assert.Equal(0.0, m.Mae, 6);
        Assert.Equal(0.0, m.Rmse, 6);
        Assert.NotNull(m.Bias);
        Assert.Equal(0.0, m.Bias!.Value, 6);
        Assert.Equal(0.0, m.BiasAbs, 6);
        Assert.NotNull(m.Mape);
        Assert.Equal(0.0, m.Mape!.Value, 6);
        Assert.Equal(0.0, m.OverPct, 6);
        Assert.Equal(0.0, m.UnderPct, 6);
        Assert.Equal(3, m.N);
    }

    // All-zero actuals => sa = 0 => Wmape and Bias are null (never infinite).
    // Every actual is zero so no mape term is collected => Mape null.
    // actual=[0,0,0], pred=[1,2,3]:
    //   ae=[1,2,3] mean=2; se=[1,4,9] mean=14/3; diff=[1,2,3] mean=2
    //   over = 3 (all f>a), under = 0
    [Fact]
    public void Compute_AllZeroActuals_NullPercentageMetrics()
    {
        var actual = new double[] { 0, 0, 0 };
        var pred = new double[] { 1, 2, 3 };

        var m = ForecastMetrics.Compute(actual, pred);

        Assert.Null(m.Wmape);
        Assert.Null(m.Bias);
        Assert.Null(m.Mape);
        Assert.Equal(2.0, m.Mae, 6);
        Assert.Equal(Math.Sqrt(14.0 / 3.0), m.Rmse, 6);
        Assert.Equal(2.0, m.BiasAbs, 6);
        Assert.Equal(100.0, m.OverPct, 6);
        Assert.Equal(0.0, m.UnderPct, 6);
        Assert.Equal(3, m.N);
    }

    // Mixed zero/non-zero actuals: Mape averages only over the non-zero actuals,
    // while Wmape/Bias use the full actual sum.
    // actual=[0,10,20], pred=[5,12,18]:
    //   ae=[5,2,2] sum=9; se=[25,4,4] sum=33; diff=[5,2,-2] sum=5; sa=30
    //   over = 2 (5>0, 12>10), under = 1 (18<20)
    //   mape terms (a!=0) = [2/10, 2/20] = [0.2, 0.1] mean=0.15
    [Fact]
    public void Compute_MixedZeroAndNonZeroActuals_MapeOverNonZeroOnly()
    {
        var actual = new double[] { 0, 10, 20 };
        var pred = new double[] { 5, 12, 18 };

        var m = ForecastMetrics.Compute(actual, pred);

        Assert.NotNull(m.Wmape);
        Assert.Equal(9.0 / 30.0 * 100.0, m.Wmape!.Value, 6); // 30.0
        Assert.Equal(9.0 / 3.0, m.Mae, 6);                   // 3.0
        Assert.Equal(Math.Sqrt(33.0 / 3.0), m.Rmse, 6);      // sqrt(11)
        Assert.NotNull(m.Bias);
        Assert.Equal(5.0 / 30.0 * 100.0, m.Bias!.Value, 6);  // 16.666667
        Assert.Equal(5.0 / 3.0, m.BiasAbs, 6);               // 1.666667
        Assert.NotNull(m.Mape);
        Assert.Equal(0.15 * 100.0, m.Mape!.Value, 6);        // 15.0
        Assert.Equal(2.0 / 3.0 * 100.0, m.OverPct, 6);
        Assert.Equal(1.0 / 3.0 * 100.0, m.UnderPct, 6);
        Assert.Equal(3, m.N);
    }

    // pred longer than actual is allowed; only the first actual.Count entries are used.
    // actual=[10,20], pred=[11,19,100] (100 ignored):
    //   ae=[1,1] sum=2; se=[1,1] sum=2; diff=[1,-1] sum=0; sa=30
    //   over = 1 (11>10), under = 1 (19<20)
    //   mape terms = [1/10, 1/20] = [0.1, 0.05] mean=0.075
    [Fact]
    public void Compute_PredLongerThanActual_UsesOnlyLeadingEntries()
    {
        var actual = new double[] { 10, 20 };
        var pred = new double[] { 11, 19, 100 };

        var m = ForecastMetrics.Compute(actual, pred);

        Assert.NotNull(m.Wmape);
        Assert.Equal(2.0 / 30.0 * 100.0, m.Wmape!.Value, 6); // 6.666667
        Assert.Equal(1.0, m.Mae, 6);
        Assert.Equal(1.0, m.Rmse, 6);
        Assert.NotNull(m.Bias);
        Assert.Equal(0.0, m.Bias!.Value, 6);
        Assert.Equal(0.0, m.BiasAbs, 6);
        Assert.NotNull(m.Mape);
        Assert.Equal(0.075 * 100.0, m.Mape!.Value, 6);       // 7.5
        Assert.Equal(50.0, m.OverPct, 6);
        Assert.Equal(50.0, m.UnderPct, 6);
        Assert.Equal(2, m.N);
    }

    // Empty inputs: n = 0. pred.Count (0) is not < actual.Count (0) so no throw.
    // sa = 0 => Wmape/Bias null; empty mape => Mape null; means of empty => 0;
    // n > 0 is false so Over/UnderPct are 0.
    [Fact]
    public void Compute_EmptyInputs_ZeroAndNullMetrics()
    {
        var actual = Array.Empty<double>();
        var pred = Array.Empty<double>();

        var m = ForecastMetrics.Compute(actual, pred);

        Assert.Null(m.Wmape);
        Assert.Null(m.Bias);
        Assert.Null(m.Mape);
        Assert.Equal(0.0, m.Mae, 6);
        Assert.Equal(0.0, m.Rmse, 6);
        Assert.Equal(0.0, m.BiasAbs, 6);
        Assert.Equal(0.0, m.OverPct, 6);
        Assert.Equal(0.0, m.UnderPct, 6);
        Assert.Equal(0, m.N);
    }

    [Fact]
    public void Compute_PredShorterThanActual_ThrowsArgumentException()
    {
        var actual = new double[] { 10, 20, 30 };
        var pred = new double[] { 1, 2 };

        var ex = Assert.Throws<ArgumentException>(() => ForecastMetrics.Compute(actual, pred));
        Assert.Equal("pred", ex.ParamName);
    }
}
