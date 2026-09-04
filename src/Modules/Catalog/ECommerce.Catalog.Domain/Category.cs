namespace ECommerce.Catalog.Domain;

/// <summary>A named grouping a customer browses by. Holds many products; a product may belong to many.</summary>
public sealed class Category
{
    private readonly List<Product> _products = [];

    private Category(Guid id, string name, string slug)
    {
        Id = id;
        Name = name;
        Slug = slug;
    }

    private Category() { Name = null!; Slug = null!; } // EF

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Slug { get; private set; }

    public IReadOnlyCollection<Product> Products => _products;

    public static Category Create(Guid id, string name, string slug)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A category needs a name.", nameof(name));
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("A category needs a slug.", nameof(slug));

        return new Category(id, name.Trim(), slug.Trim().ToLowerInvariant());
    }
}
