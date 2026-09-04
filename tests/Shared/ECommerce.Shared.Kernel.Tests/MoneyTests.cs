using ECommerce.Shared.Kernel;
using FluentAssertions;

namespace ECommerce.Shared.Kernel.Tests;

/// <summary>
/// TXN-006 / FR-032 / FR-033: money is a whole number of the smallest currency unit.
/// For VND the minor unit is the dong itself, so the scale is 1, not 100.
/// </summary>
public class MoneyTests
{
    [Fact]
    public void Carries_an_integer_minor_amount_and_a_currency()
    {
        var money = Money.FromMinor(50_000, "VND");
        money.AmountMinor.Should().Be(50_000L);
        money.CurrencyCode.Should().Be("VND");
    }

    [Fact]
    public void Rejects_a_currency_code_that_is_not_three_letters()
    {
        var act = () => Money.FromMinor(1, "DONG");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Addition_stays_integral()
    {
        (Money.FromMinor(50_000, "VND") + Money.FromMinor(70_000, "VND"))
            .AmountMinor.Should().Be(120_000L);
    }

    [Fact]
    public void Subtraction_stays_integral()
    {
        (Money.FromMinor(250_000, "VND") - Money.FromMinor(70_000, "VND"))
            .AmountMinor.Should().Be(180_000L);
    }

    [Fact]
    public void Rejects_arithmetic_across_currencies()
    {
        var act = () => Money.FromMinor(1, "VND") + Money.FromMinor(1, "USD");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Compares_by_amount_within_one_currency()
    {
        (Money.FromMinor(180_000, "VND") < Money.FromMinor(250_000, "VND")).Should().BeTrue();
        (Money.FromMinor(250_000, "VND") >= Money.FromMinor(250_000, "VND")).Should().BeTrue();
    }

    [Fact]
    public void Rejects_comparison_across_currencies()
    {
        var act = () => Money.FromMinor(1, "VND") < Money.FromMinor(1, "USD");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Round_trips_through_its_minor_amount_without_drift()
    {
        // FR-030: the value stored, compared and displayed is the same integer.
        const long stored = 199_999_999_999L;
        Money.FromMinor(stored, "VND").AmountMinor.Should().Be(stored);
    }

    [Fact]
    public void Is_never_expressed_as_a_floating_point_number()
    {
        // TXN-006 in the type system: the only numeric member is a 64-bit integer.
        typeof(Money).GetProperty(nameof(Money.AmountMinor))!.PropertyType.Should().Be(typeof(long));
    }

    [Fact]
    public void Two_amounts_with_the_same_value_and_currency_are_equal()
    {
        Money.FromMinor(500, "VND").Should().Be(Money.FromMinor(500, "VND"));
    }
}
