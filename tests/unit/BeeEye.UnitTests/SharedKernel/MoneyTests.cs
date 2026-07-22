using BeeEye.Shared.Primitives;
using Xunit;

namespace BeeEye.UnitTests.SharedKernel;

public sealed class MoneyTests
{
    [Fact]
    public void Add_SameCurrency_SumsAmounts()
    {
        var result = Money.Sar(100m).Add(Money.Sar(50.25m));

        Assert.Equal(150.25m, result.Amount);
        Assert.Equal("SAR", result.Currency);
    }

    [Fact]
    public void Add_DifferentCurrency_Throws()
    {
        var sar = Money.Sar(100m);
        var usd = new Money(100m, "USD");

        Assert.Throws<InvalidOperationException>(() => sar.Add(usd));
    }

    [Fact]
    public void Constructor_NormalisesCurrencyToUpperCase()
    {
        var money = new Money(10m, "sar");

        Assert.Equal("SAR", money.Currency);
    }

    [Fact]
    public void Constructor_BlankCurrency_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Money(10m, " "));
    }

    [Fact]
    public void Amount_UsesDecimal_NotFloatingPoint()
    {
        // 0.1 + 0.2 must equal exactly 0.3 with decimal money — the platform's
        // financial-integrity rule (never float for money).
        var result = Money.Sar(0.1m).Add(Money.Sar(0.2m));

        Assert.Equal(0.3m, result.Amount);
    }
}
