namespace ECommerce.Catalog.Domain;

/// <summary>
/// One picture of a product, with a position in the gallery. Exactly one image per product
/// is primary and used in listings — enforced by a partial unique index rather than a
/// NOT NULL on the product, because a product with no images must still render (FR-009).
/// </summary>
public sealed class ProductImage
{
    private ProductImage(Guid id, Guid productId, string url, int position, bool isPrimary)
    {
        Id = id;
        ProductId = productId;
        Url = url;
        Position = position;
        IsPrimary = isPrimary;
    }

    private ProductImage() { Url = null!; } // EF

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string Url { get; private set; }
    public int Position { get; private set; }
    public bool IsPrimary { get; private set; }

    public static ProductImage Create(Guid id, Guid productId, string url, int position, bool isPrimary)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("An image needs a url.", nameof(url));
        if (position < 0)
            throw new ArgumentOutOfRangeException(nameof(position), position, "Gallery position cannot be negative.");

        return new ProductImage(id, productId, url.Trim(), position, isPrimary);
    }
}
