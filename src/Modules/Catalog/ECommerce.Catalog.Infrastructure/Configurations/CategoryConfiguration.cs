using ECommerce.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Catalog.Infrastructure.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("category");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(c => c.Slug).HasColumnName("slug").HasMaxLength(200).IsRequired();

        builder.HasIndex(c => c.Name).IsUnique().HasDatabaseName("ux_category_name");
        builder.HasIndex(c => c.Slug).IsUnique().HasDatabaseName("ux_category_slug");

        // Products is the inverse skip navigation; the Product side configures field access
        // for the pair, and EF discovers the backing field by convention here.
    }
}
