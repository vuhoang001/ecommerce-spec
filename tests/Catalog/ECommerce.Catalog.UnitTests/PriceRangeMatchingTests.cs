using ECommerce.Catalog.Application.Contracts;
using ECommerce.Catalog.Application.Filter;
using FluentAssertions;

namespace ECommerce.Catalog.UnitTests;

/// <summary>
/// FR-022, FR-023, FR-024, FR-025 — range validation, including bounds exactly equal to the
/// minimum and the maximum (FR-023 is inclusive).
/// </summary>
public class PriceRangeMatchingTests
{
    private static PriceRangeValidator.Range Range(long? min, long? max) => new(min, max);

    [Fact]
    public void A_well_formed_range_is_accepted()
    {
        PriceRangeValidator.Validate(Range(50_000, 200_000)).Valid.Should().BeTrue();
    }

    [Fact]
    public void Equal_bounds_are_accepted_because_the_range_is_inclusive()
    {
        // FR-023: a range of exactly one price is legitimate.
        PriceRangeValidator.Validate(Range(180_000, 180_000)).Valid.Should().BeTrue();
    }

    [Fact]
    public void An_inverted_range_is_rejected_with_a_reason_and_not_an_empty_result()
    {
        var (valid, reasonCode, detail) = PriceRangeValidator.Validate(Range(200_000, 50_000));

        valid.Should().BeFalse();
        reasonCode.Should().Be(ReasonCodes.MinExceedsMax, "FR-022 names the problem");
        detail.Should().Contain("200000").And.Contain("50000");
    }

    [Theory]
    [InlineData(-1L, null)]
    [InlineData(null, -1L)]
    [InlineData(-5L, -1L)]
    public void A_negative_bound_is_rejected(long? min, long? max)
    {
        var (valid, reasonCode, _) = PriceRangeValidator.Validate(Range(min, max));

        valid.Should().BeFalse();
        reasonCode.Should().Be(ReasonCodes.NegativePriceBound);
    }

    [Theory]
    [InlineData(50_000L, null)]
    [InlineData(null, 200_000L)]
    [InlineData(null, null)]
    public void An_omitted_bound_is_unbounded_on_that_side(long? min, long? max)
    {
        // FR-024
        PriceRangeValidator.Validate(Range(min, max)).Valid.Should().BeTrue();
    }

    [Fact]
    public void Zero_is_a_valid_bound()
    {
        PriceRangeValidator.Validate(Range(0, 0)).Valid.Should().BeTrue();
    }
}
