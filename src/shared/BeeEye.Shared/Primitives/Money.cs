using System.Globalization;

namespace BeeEye.Shared.Primitives;

/// <summary>
/// A monetary amount with an explicit currency. Money is stored as <see cref="decimal"/> —
/// never floating point — per the platform's financial-integrity rules. Arithmetic
/// across differing currencies is rejected rather than silently coerced.
/// </summary>
public readonly record struct Money
{
    public Money(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency is required.", nameof(currency));
        }

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public decimal Amount { get; }
    public string Currency { get; }

    /// <summary>Zero in the given currency (default SAR — the ADMC reporting currency).</summary>
    public static Money Zero(string currency = "SAR") => new(0m, currency);

    /// <summary>Convenience constructor for Saudi Riyal, the dataset's currency.</summary>
    public static Money Sar(decimal amount) => new(amount, "SAR");

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot combine money of different currencies ({Currency} vs {other.Currency}).");
        }
    }

    public override string ToString() => $"{Amount.ToString("0.00", CultureInfo.InvariantCulture)} {Currency}";
}
