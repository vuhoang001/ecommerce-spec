using ECommerce.Catalog.Application.Ports;
using ECommerce.Catalog.Domain;
using ECommerce.Catalog.Infrastructure;
using ECommerce.Catalog.Infrastructure.Promotion;
using ECommerce.Promotion.Contracts.V1;
using ProtoMoney = ECommerce.Promotion.Contracts.V1.Money;
using KernelMoney = ECommerce.Shared.Kernel.Money;
using ECommerce.Shared.Kernel.Primitives;
using FluentAssertions;
using Google.Protobuf.WellKnownTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ECommerce.Catalog.ResilienceTests;

/// <summary>
/// FR-031, research.md R12 — several instances starting together must seed the discount copy
/// exactly once. A PostgreSQL advisory lock is the arbiter; the seed itself is an idempotent
/// upsert so losing the lock costs duplicated work rather than a wrong projection.
/// </summary>
[Collection("resilience")]
public class ProjectionSeedTests(ResilienceFixture fixture)
{
    private sealed class CountingPromotion(List<AppliedDiscount> discounts) : IPromotionPricingPort
    {
        public int ListCalls;

        public Task<PricingResult> GetPricingAsync(Guid productId, long originalPriceMinor,
            string currencyCode, CancellationToken ct = default) =>
            Task.FromResult(new PricingResult { Unavailable = new PricingUnavailable() });

        public Task<IReadOnlyList<AppliedDiscount>> ListActiveDiscountsAsync(
            int pageSize, string? pageToken, CancellationToken ct = default)
        {
            Interlocked.Increment(ref ListCalls);
            return Task.FromResult<IReadOnlyList<AppliedDiscount>>(discounts);
        }
    }

    private async Task<Guid> SeedProductAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var p = Product.Create(Guid.NewGuid(), "Seeded", null, KernelMoney.FromMinor(250_000, "VND"),
            1, ProductStatus.Active);
        db.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    private DiscountProjectionSeeder NewSeeder(CatalogDbContext db, CountingPromotion promotion) =>
        new(db, promotion, new SystemClock(), NullLogger<DiscountProjectionSeeder>.Instance);

    [Fact]
    public async Task Two_instances_starting_cold_seed_the_copy_exactly_once()
    {
        var productId = await SeedProductAsync();
        var discounts = new List<AppliedDiscount>
        {
            new()
            {
                ProductId = productId.ToString(),
                PromotionId = Guid.NewGuid().ToString(),
                DiscountedPrice = new ProtoMoney { AmountMinor = 180_000, CurrencyCode = "VND" },
                OccurredAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
            }
        };

        var promotionA = new CountingPromotion(discounts);
        var promotionB = new CountingPromotion(discounts);

        using var scopeA = fixture.Services.CreateScope();
        using var scopeB = fixture.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<CatalogDbContext>();

        // Both "instances" start at the same moment, as they would behind a load balancer.
        var results = await Task.WhenAll(
            NewSeeder(dbA, promotionA).SeedAsync(),
            NewSeeder(dbB, promotionB).SeedAsync());

        using var check = fixture.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<CatalogDbContext>();

        (await db.DiscountProjections.CountAsync(d => d.ProductId == productId))
            .Should().Be(1, "the advisory lock means one instance seeds, not both");

        results.Count(r => r > 0).Should().BeLessThanOrEqualTo(1,
            "at most one instance performs the seed");
    }

    [Fact]
    public async Task Seeding_twice_in_sequence_is_idempotent()
    {
        var productId = await SeedProductAsync();
        var discounts = new List<AppliedDiscount>
        {
            new()
            {
                ProductId = productId.ToString(),
                PromotionId = Guid.NewGuid().ToString(),
                DiscountedPrice = new ProtoMoney { AmountMinor = 170_000, CurrencyCode = "VND" },
                OccurredAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
            }
        };

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var promotion = new CountingPromotion(discounts);

        await NewSeeder(db, promotion).SeedAsync();
        await NewSeeder(db, promotion).SeedAsync();

        (await db.DiscountProjections.CountAsync(d => d.ProductId == productId))
            .Should().Be(1, "the seed is an upsert, so a repeat costs work and not correctness");
    }
}
