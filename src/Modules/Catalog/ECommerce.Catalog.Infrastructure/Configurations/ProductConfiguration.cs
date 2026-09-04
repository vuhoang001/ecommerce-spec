using ECommerce.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Catalog.Infrastructure.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("product", t =>
        {
            t.HasCheckConstraint("ck_product_price_non_negative", "price_minor >= 0");
            t.HasCheckConstraint("ck_product_stock_non_negative", "stock_quantity >= 0");
        });
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(Product.MaxNameLength).IsRequired();
        builder.Property(p => p.Description).HasColumnName("description");

        // TXN-006: the money columns are bigint minor units plus a currency code.
        builder.Property<long>("_priceMinor").HasColumnName("price_minor").HasColumnType("bigint").IsRequired();
        builder.Property<string>("_currencyCode").HasColumnName("currency_code")
            .HasColumnType("char(3)").IsRequired();
        builder.Ignore(p => p.Price);

        builder.Property(p => p.StockQuantity).HasColumnName("stock_quantity").IsRequired();
        builder.Property(p => p.Status).HasColumnName("status")
            .HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();

        // FR-017 — generated once by the database, never written by the application.
        builder.Property<string>("NameNormalized")
            .HasColumnName("name_normalized")
            .HasComputedColumnSql("catalog.normalise_name(name)", stored: true);

        builder.Ignore(p => p.IsOutOfStock);
        builder.Ignore(p => p.IsVisibleToCustomers);
        builder.Ignore(p => p.PrimaryImage);

        // Listing path (FR-003) and default newest-first ordering.
        builder.HasIndex(p => new { p.Status, p.CreatedAt }).HasDatabaseName("ix_product_status_created_at");
        // Price range filter (FR-026).
        builder.HasIndex("Status", "_priceMinor").HasDatabaseName("ix_product_status_price_minor");

        builder.HasMany(p => p.Images)
            .WithOne()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Categories)
            .WithMany(c => c.Products)
            .UsingEntity(join =>
            {
                join.ToTable("product_category");
                join.Property("ProductsId").HasColumnName("product_id");
                join.Property("CategoriesId").HasColumnName("category_id");
                // PK (product_id, category_id) is what makes FR-006 hold: a product cannot
                // appear twice within one category's listing.
            });

        // Categories is a skip navigation (many-to-many), not a plain navigation.
        builder.Metadata.FindSkipNavigation(nameof(Product.Categories))
            ?.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Product.Images))
            ?.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
