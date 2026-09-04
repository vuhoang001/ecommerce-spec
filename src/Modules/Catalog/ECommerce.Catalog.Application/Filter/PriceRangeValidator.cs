using ECommerce.Catalog.Application.Contracts;

namespace ECommerce.Catalog.Application.Filter;

/// <summary>
/// FR-022, FR-024, FR-025 — a price range is validated before any query runs, and an invalid
/// range is a stated error rather than an empty result (FR-029).
/// </summary>
public static class PriceRangeValidator
{
    public sealed record Range(long? MinMinor, long? MaxMinor);

    public static (bool Valid, string? ReasonCode, string? Detail) Validate(Range range)
    {
        // FR-025: a negative bound is meaningless for a price.
        if (range.MinMinor is < 0 || range.MaxMinor is < 0)
            return (false, ReasonCodes.NegativePriceBound,
                "A price bound cannot be negative.");

        // FR-022: an inverted range is an error. Returning an empty result in its place is
        // FORBIDDEN — the customer would read it as "nothing matches" rather than "bad input".
        if (range is { MinMinor: { } min, MaxMinor: { } max } && min > max)
            return (false, ReasonCodes.MinExceedsMax,
                $"The minimum ({min}) exceeds the maximum ({max}).");

        // FR-024: an omitted bound is unbounded on that side, which is valid.
        return (true, null, null);
    }
}
