using ECommerce.Catalog.Domain;
using ECommerce.Shared.Kernel;
using FluentAssertions;

namespace ECommerce.Catalog.UnitTests;

/// <summary>TST-002: domain invariant tests for every invariant the aggregate enforces.</summary>
public class ProductInvariantTests
{
    private static Product Create(string name = "Cà phê sữa đá", long price = 50_000, int stock = 3)
        => Product.Create(Guid.NewGuid(), name, "desc", Money.FromMinor(price, "VND"), stock,
            ProductStatus.Active, CreatedAt);

    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Requires_a_non_empty_name()
    {
        var act = () => Create(name: "   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Trims_the_name()
    {
        Create(name: "  Trà đá  ").Name.Should().Be("Trà đá");
    }

    [Fact]
    public void Rejects_a_name_longer_than_200_characters()
    {
        var act = () => Create(name: new string('x', 201));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rejects_a_negative_price()
    {
        var act = () => Create(price: -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Allows_a_zero_price()
    {
        Create(price: 0).Price.AmountMinor.Should().Be(0L);
    }

    [Fact]
    public void Rejects_a_negative_stock_quantity()
    {
        var act = () => Create(stock: -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Is_out_of_stock_when_the_quantity_reaches_zero()
    {
        // FR-005: the product stays listed; only the label changes.
        Create(stock: 0).IsOutOfStock.Should().BeTrue();
        Create(stock: 1).IsOutOfStock.Should().BeFalse();
    }

    [Theory]
    [InlineData(ProductStatus.Active, true)]
    [InlineData(ProductStatus.Draft, false)]
    [InlineData(ProductStatus.Hidden, false)]
    [InlineData(ProductStatus.Discontinued, false)]
    public void Is_visible_to_customers_only_while_active(ProductStatus status, bool visible)
    {
        // FR-001 / SC-002
        Product.Create(Guid.NewGuid(), "x", null, Money.FromMinor(1, "VND"), 1, status, CreatedAt)
            .IsVisibleToCustomers.Should().Be(visible);
    }

    [Fact]
    public void Keeps_the_currency_of_its_price()
    {
        Create().Price.CurrencyCode.Should().Be("VND");
    }
}
