using System.Collections.Generic;
using BeeEye.Analytics;
using Xunit;

namespace BeeEye.Analytics.Tests;

/// <summary>Deterministic tests for the Pearson correlation + CV helpers added for UC6/UC7.</summary>
public class StatisticsCorrelationTests
{
    [Fact]
    public void Correlation_PerfectPositive_IsOne()
    {
        var r = Statistics.Correlation([1, 2, 3, 4], [2, 4, 6, 8]);
        Assert.NotNull(r);
        Assert.Equal(1.0, r!.Value, 10);
    }

    [Fact]
    public void Correlation_PerfectNegative_IsMinusOne()
    {
        var r = Statistics.Correlation([1, 2, 3, 4], [8, 6, 4, 2]);
        Assert.NotNull(r);
        Assert.Equal(-1.0, r!.Value, 10);
    }

    [Fact]
    public void Correlation_ZeroVariance_IsNull()
    {
        // x has no variance -> correlation undefined.
        Assert.Null(Statistics.Correlation([5, 5, 5], [1, 2, 3]));
        Assert.Null(Statistics.Correlation([1, 2, 3], [7, 7, 7]));
    }

    [Fact]
    public void Correlation_TooShort_IsNull()
    {
        Assert.Null(Statistics.Correlation([1], [2]));
        Assert.Null(Statistics.Correlation([], []));
    }

    [Fact]
    public void Correlation_KnownModerate_MatchesHandValue()
    {
        // x=[1,2,3,4,5], y=[2,4,5,4,5]:
        // mean x=3, mean y=4; Sxy = (-2)(-2)+(-1)(0)+0+1*0+2*1 = 4+0+0+0+2 = 6
        // Sxx = 4+1+0+1+4 = 10 ; Syy = 4+0+1+0+1 = 6 ; r = 6/sqrt(60) = 0.7745966692
        var r = Statistics.Correlation([1, 2, 3, 4, 5], [2, 4, 5, 4, 5]);
        Assert.NotNull(r);
        Assert.Equal(0.7745966692, r!.Value, 8);
    }

    [Fact]
    public void Correlation_IsClampedToUnitRange()
    {
        var r = Statistics.Correlation([1, 2, 3, 4], [2, 4, 6, 8]);
        Assert.InRange(r!.Value, -1.0, 1.0);
    }

    [Theory]
    [InlineData(new[] { 10.0, 10.0, 10.0 }, 0.0)] // zero std -> cv 0
    [InlineData(new[] { 0.0, 0.0 }, 0.0)]         // mean 0 -> guarded to 0
    public void CoefficientOfVariation_EdgeCases(double[] values, double expected)
    {
        Assert.Equal(expected, Statistics.CoefficientOfVariation(values), 10);
    }

    [Fact]
    public void CoefficientOfVariation_KnownValue()
    {
        // values [2,4,6]: mean 4, population std = sqrt(((-2)^2+0+2^2)/3)=sqrt(8/3)=1.6329931619
        // cv = 1.6329931619 / 4 = 0.4082482905
        Assert.Equal(0.4082482905, Statistics.CoefficientOfVariation([2, 4, 6]), 8);
    }
}
