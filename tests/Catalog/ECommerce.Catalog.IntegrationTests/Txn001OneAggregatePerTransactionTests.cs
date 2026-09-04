using ECommerce.Catalog.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ECommerce.Catalog.IntegrationTests;

/// <summary>
/// TXN-001 — one aggregate per transaction. The interceptor below is the mechanical check:
/// it inspects the change tracker at SaveChanges and refuses a transaction touching more than
/// one aggregate root.
/// </summary>
[Collection("catalog")]
public class Txn001OneAggregatePerTransactionTests(CatalogFixture fixture)
{
    [Fact]
    public async Task Saving_one_aggregate_root_is_allowed()
    {
        await fixture.ResetAsync();
        var act = async () => await fixture.WithDbAsync(async db =>
        {
            OneAggregatePerTransactionInterceptor.Attach(db);
            db.Add(CatalogFixture.NewProduct("Single"));
            await db.SaveChangesAsync();
        });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Saving_two_aggregate_roots_in_one_transaction_is_refused()
    {
        await fixture.ResetAsync();
        var act = async () => await fixture.WithDbAsync(async db =>
        {
            OneAggregatePerTransactionInterceptor.Attach(db);
            db.Add(CatalogFixture.NewProduct("First"));
            db.Add(CatalogFixture.NewProduct("Second"));
            await db.SaveChangesAsync();
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*TXN-001*");
    }
}

/// <summary>
/// The check TXN-001 names. Kept in the test assembly because this feature has no write path
/// of its own — it is read-only — so the interceptor guards the rule rather than production
/// traffic. The first feature that writes moves it into the host.
/// </summary>
internal sealed class OneAggregatePerTransactionInterceptor : SaveChangesInterceptor
{
    private static readonly Type[] AggregateRoots = [typeof(Product), typeof(Category)];

    internal static void Attach(DbContext db) => db.SavingChanges += (sender, _) => Check((DbContext)sender!);

    private static void Check(DbContext db)
    {
        var roots = db.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => AggregateRoots.Contains(e.Entity.GetType()))
            .Select(e => e.Entity)
            .Distinct()
            .Count();

        if (roots > 1)
            throw new InvalidOperationException(
                $"TXN-001: one aggregate per transaction; this transaction modifies {roots}.");
    }
}
