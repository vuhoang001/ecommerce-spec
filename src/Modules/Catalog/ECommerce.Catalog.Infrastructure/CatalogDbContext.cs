using ECommerce.Catalog.Domain;
using ECommerce.Shared.Messaging.Inbox;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Catalog.Infrastructure;

/// <summary>
/// The catalog module's only database context. It owns exactly one schema and maps no table
/// outside it (DAT-001).
/// </summary>
public class CatalogDbContext : DbContext
{
    public const string Schema = "catalog";

    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    /// <summary>The spec's discount copy (FR-014). Catalog's own data, never Promotion's.</summary>
    public DbSet<DiscountProjection> DiscountProjections => Set<DiscountProjection>();

    /// <summary>REL-003 — this module's own inbox, in this module's own schema (DAT-001).</summary>
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // The name normalisation function is now called directly from the Dapper read paths
        // (DAT-004), so EF no longer needs a DbFunction mapping for it.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());

        // Defence in depth only. Under DAT-004 reads go through Dapper, which does not see this
        // filter — DAT-005's shared fragment is what actually guarantees FR-001 and SC-002 on the
        // read paths now. This stays because any EF read that ever appears should still fail
        // closed rather than leak a concealed product.
        modelBuilder.Entity<Product>().HasQueryFilter(p => p.Status == ProductStatus.Active);

        base.OnModelCreating(modelBuilder);
    }
}
