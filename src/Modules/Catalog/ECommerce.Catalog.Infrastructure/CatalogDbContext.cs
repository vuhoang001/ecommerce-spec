using ECommerce.Catalog.Application.Search;
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

        // research.md R3 — the same normalisation applies to the stored name (as a generated
        // column) and to the keyword (as a call), so a match works in both directions.
        modelBuilder
            .HasDbFunction(typeof(CatalogFunctions).GetMethod(nameof(CatalogFunctions.Normalise))!)
            .HasName("normalise_name")
            .HasSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());

        // FR-001 / SC-002: only Active products ever reach a customer, on every read path.
        // A global filter fails closed; filtering at each call site is one forgotten call
        // site away from leaking a hidden product (research.md R9).
        modelBuilder.Entity<Product>().HasQueryFilter(p => p.Status == ProductStatus.Active);

        base.OnModelCreating(modelBuilder);
    }
}
