namespace ECommerce.Catalog.Application.Contracts;

/// <summary>Whole minor units plus a currency code. Never fractional, never converted (FR-032, FR-033).</summary>
public sealed record MoneyDto(long AmountMinor, string CurrencyCode);

/// <summary>
/// The price a customer sees. <paramref name="Original"/> is present only when a discount
/// applies and is shown struck through (FR-010). <paramref name="IsOutOfDate"/> marks a price
/// that came from the discount copy, or an undiscounted price shown because none was available
/// (FR-013, FR-015).
/// </summary>
public sealed record PriceDisplayDto(MoneyDto Current, MoneyDto? Original, bool IsOutOfDate);

public sealed record CategoryRefDto(Guid Id, string Name, string Slug);

public sealed record ProductImageDto(string Url, int Position);

public sealed record ProductSummaryDto(
    Guid Id,
    string Name,
    string? PrimaryImageUrl,
    PriceDisplayDto Price,
    bool IsOutOfStock,
    bool MatchedOnDiscountedPriceOnly = false);

public sealed record ProductDetailDto(
    Guid Id,
    string Name,
    string? PrimaryImageUrl,
    PriceDisplayDto Price,
    bool IsOutOfStock,
    string? Description,
    int StockQuantity,
    IReadOnlyList<CategoryRefDto> Categories,
    IReadOnlyList<ProductImageDto> Images);

public sealed record ProductPageDto(
    IReadOnlyList<ProductSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    string? EmptyReason);

/// <summary>Reason codes from contracts/catalog-storefront.openapi.yaml.</summary>
public static class ReasonCodes
{
    public const string MinExceedsMax = "MIN_EXCEEDS_MAX";
    public const string NegativePriceBound = "NEGATIVE_PRICE_BOUND";
    public const string EmptyKeyword = "EMPTY_KEYWORD";
    public const string ProductNotFound = "PRODUCT_NOT_FOUND";
    public const string CategoryNotFound = "CATEGORY_NOT_FOUND";
    public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";

    public const string NoProductsInCategory = "NO_PRODUCTS_IN_CATEGORY";
    public const string NoMatches = "NO_MATCHES";
    public const string PageBeyondLast = "PAGE_BEYOND_LAST";
}

/// <summary>Paging defaults from research.md R6.</summary>
public static class Paging
{
    public const int DefaultPageSize = 24;
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) Normalise(int? page, int? pageSize) =>
        (Math.Max(1, page ?? 1), Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));
}
