using ECommerce.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Catalog.Infrastructure.Configurations;

public sealed class DiscountProjectionConfiguration : IEntityTypeConfiguration<DiscountProjection>
{
    public void Configure(EntityTypeBuilder<DiscountProjection> builder)
    {
        builder.ToTable("discount_projection", t =>
            t.HasCheckConstraint("ck_discount_projection_non_negative", "discounted_price_minor >= 0"));

        builder.HasKey(d => d.ProductId);

        builder.Property(d => d.ProductId).HasColumnName("product_id");
        builder.Property(d => d.PromotionId).HasColumnName("promotion_id").IsRequired();

        // TXN-006: bigint minor units, never a floating-point type.
        builder.Property<long>("_discountedPriceMinor")
            .HasColumnName("discounted_price_minor").HasColumnType("bigint").IsRequired();
        builder.Property<string>("_currencyCode")
            .HasColumnName("currency_code").HasColumnType("char(3)").IsRequired();
        builder.Ignore(d => d.DiscountedPrice);

        builder.Property(d => d.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(d => d.RetrievedAt).HasColumnName("retrieved_at").IsRequired();

        // FR-026 — the filter reads this column alongside product.price_minor.
        builder.HasIndex("_discountedPriceMinor").HasDatabaseName("ix_discount_projection_price");
        builder.HasIndex(d => d.RetrievedAt).HasDatabaseName("ix_discount_projection_retrieved_at");

        // DAT-002: the foreign key stays inside the catalog schema. The row holds a COPY of a
        // Promotion fact, not a reference into Promotion's tables.
        builder.HasOne<Product>()
            .WithOne()
            .HasForeignKey<DiscountProjection>(d => d.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
