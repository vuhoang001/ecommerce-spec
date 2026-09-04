namespace ECommerce.Catalog.Application.Reads;

/// <summary>Flat row shape the Dapper read paths materialise (DAT-004).</summary>
public sealed class ProductReadRow
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public long PriceMinor { get; init; }
    public string CurrencyCode { get; init; } = "VND";
    public int StockQuantity { get; init; }
    public string? PrimaryImageUrl { get; init; }
    public long? DiscountedPriceMinor { get; init; }
}
