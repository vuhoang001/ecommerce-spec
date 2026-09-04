using ECommerce.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Catalog.Infrastructure.Configurations;

public sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("product_image");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(i => i.Url).HasColumnName("url").IsRequired();
        builder.Property(i => i.Position).HasColumnName("position").IsRequired();
        builder.Property(i => i.IsPrimary).HasColumnName("is_primary").IsRequired();

        builder.HasIndex(i => new { i.ProductId, i.Position })
            .IsUnique().HasDatabaseName("ux_product_image_position");

        // Exactly one primary image per product, when the product has any at all.
        builder.HasIndex(i => i.ProductId)
            .IsUnique()
            .HasFilter("is_primary")
            .HasDatabaseName("ux_product_image_primary");
    }
}
