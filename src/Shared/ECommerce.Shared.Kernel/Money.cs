namespace ECommerce.Shared.Kernel;

/// <summary>
/// A monetary amount as a whole number of the smallest currency unit (MON-001).
/// For VND the minor unit is the dong itself, so the scale is 1 rather than 100 —
/// the type carries the amount as given and never rescales it.
/// </summary>
/// <remarks>
/// The only numeric member is a 64-bit integer. Floating-point and decimal types are
/// banned from every money path by the MON-001 architecture test; wrapping the integer
/// gives that test one type to assert on instead of a rule about bare longs it cannot
/// tell apart from a stock count.
/// </remarks>
public readonly record struct Money : IComparable<Money>
{
    private Money(long amountMinor, string currencyCode)
    {
        AmountMinor = amountMinor;
        CurrencyCode = currencyCode;
    }

    /// <summary>The amount, in whole minor units. Never fractional (FR-033).</summary>
    public long AmountMinor { get; }

    /// <summary>ISO 4217 alphabetic code, upper case.</summary>
    public string CurrencyCode { get; }

    public static Money FromMinor(long amountMinor, string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Trim().Length != 3)
            throw new ArgumentException(
                $"Currency code must be three letters (ISO 4217); got '{currencyCode}'.",
                nameof(currencyCode));

        foreach (var c in currencyCode)
        {
            if (!char.IsLetter(c))
                throw new ArgumentException(
                    $"Currency code must be alphabetic; got '{currencyCode}'.", nameof(currencyCode));
        }

        return new Money(amountMinor, currencyCode.ToUpperInvariant());
    }

    public static Money Zero(string currencyCode) => FromMinor(0, currencyCode);

    public bool IsNegative => AmountMinor < 0;

    public static Money operator +(Money left, Money right)
        => new(checked(left.AmountMinor + Same(left, right).AmountMinor), left.CurrencyCode);

    public static Money operator -(Money left, Money right)
        => new(checked(left.AmountMinor - Same(left, right).AmountMinor), left.CurrencyCode);

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;
    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;
    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    public int CompareTo(Money other) => AmountMinor.CompareTo(Same(this, other).AmountMinor);

    public Money Add(Money other) => this + other;

    public Money Subtract(Money other) => this - other;

    public override string ToString() => $"{AmountMinor} {CurrencyCode}";

    private static Money Same(Money left, Money right)
    {
        if (!string.Equals(left.CurrencyCode, right.CurrencyCode, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Cannot combine {left.CurrencyCode} with {right.CurrencyCode}: the catalogue " +
                "is single-currency and never converts (FR-032).");
        return right;
    }
}
