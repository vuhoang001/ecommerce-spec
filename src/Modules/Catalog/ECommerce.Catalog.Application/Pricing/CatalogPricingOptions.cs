namespace ECommerce.Catalog.Application.Pricing;

/// <summary>FR-015 — how old a discount copy may be before it is no longer shown.</summary>
public sealed class CatalogPricingOptions
{
    /// <summary>
    /// 15 minutes, recorded as an assumption in the spec: beyond it the catalogue shows the
    /// undiscounted price rather than one it can no longer stand behind.
    /// </summary>
    public TimeSpan DiscountStalenessLimit { get; set; } = TimeSpan.FromMinutes(15);
}
