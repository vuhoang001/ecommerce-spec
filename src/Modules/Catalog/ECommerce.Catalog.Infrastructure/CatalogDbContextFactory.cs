using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerce.Catalog.Infrastructure;

/// <summary>Design-time factory so `dotnet ef` can build the model from this class library.</summary>
public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql("Host=localhost;Database=ecommerce;Username=ecommerce;Password=ecommerce",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CatalogDbContext.Schema))
            .Options;
        return new CatalogDbContext(options);
    }
}
