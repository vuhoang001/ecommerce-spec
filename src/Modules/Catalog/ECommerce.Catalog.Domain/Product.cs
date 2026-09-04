using ECommerce.Shared.Kernel;

namespace ECommerce.Catalog.Domain;

/// <summary>
/// A purchasable item. Aggregate root of the catalog module.
/// </summary>
/// <remarks>
/// The price is held as a 64-bit integer minor amount plus a currency code and surfaced as
/// <see cref="Money"/> (MON-001). Stock is read here and never written — inventory belongs
/// to another feature (spec Out of Scope).
/// </remarks>
public sealed class Product
{
    public const int MaxNameLength = 200;

    private readonly List<Category> _categories = [];
    private readonly List<ProductImage> _images = [];

    private long _priceMinor;
    private string _currencyCode = "VND";

    private Product(Guid id, string name, string? description, Money price, int stockQuantity,
        ProductStatus status, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Description = description;
        _priceMinor = price.AmountMinor;
        _currencyCode = price.CurrencyCode;
        StockQuantity = stockQuantity;
        Status = status;
        CreatedAt = createdAt;
    }

    private Product() { Name = null!; } // EF

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }

    /// <summary>Whole minor units — never fractional, never converted (FR-032, FR-033).</summary>
    public Money Price => Money.FromMinor(_priceMinor, _currencyCode);

    public int StockQuantity { get; private set; }
    public ProductStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>FR-005: the product stays listed and viewable; only the label changes.</summary>
    public bool IsOutOfStock => StockQuantity == 0;

    /// <summary>FR-001 / SC-002: only Active products ever reach a customer.</summary>
    public bool IsVisibleToCustomers => Status == ProductStatus.Active;

    public IReadOnlyCollection<Category> Categories => _categories;
    public IReadOnlyCollection<ProductImage> Images => _images;

    public ProductImage? PrimaryImage =>
        _images.FirstOrDefault(i => i.IsPrimary) ?? _images.OrderBy(i => i.Position).FirstOrDefault();

    public static Product Create(
        Guid id,
        string name,
        string? description,
        Money price,
        int stockQuantity,
        ProductStatus status,
        DateTimeOffset? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A product needs a name.", nameof(name));

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
            throw new ArgumentException(
                $"A product name is at most {MaxNameLength} characters; got {trimmed.Length}.", nameof(name));

        if (price.IsNegative)
            throw new ArgumentOutOfRangeException(nameof(price), price.AmountMinor, "A price cannot be negative.");

        if (stockQuantity < 0)
            throw new ArgumentOutOfRangeException(
                nameof(stockQuantity), stockQuantity, "A stock quantity cannot be negative.");

        return new Product(id, trimmed, description, price, stockQuantity, status,
            createdAt ?? DateTimeOffset.UtcNow);
    }

    public void AssignTo(Category category)
    {
        ArgumentNullException.ThrowIfNull(category);
        if (_categories.Any(c => c.Id == category.Id)) return; // FR-006: once per listing
        _categories.Add(category);
    }

    public void AddImage(ProductImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        _images.Add(image);
    }
}
