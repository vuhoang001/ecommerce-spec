using System.Text.Json;
using ECommerce.Catalog.Domain;
using ECommerce.Promotion.Contracts.Events;
using ECommerce.Shared.Kernel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Catalog.ContractTests;

/// <summary>
/// REL-005 — consumers are tolerant readers: an unknown field MUST be ignored, never rejected.
/// This is what lets Promotion add a field without a coordinated deploy (MSG-003).
/// </summary>
[Collection("pricing")]
public class TolerantReaderTests(PricingFixture fixture)
{
    [Fact]
    public void A_payload_carrying_an_added_field_deserialises_without_error()
    {
        var json = """
        {
          "messageId": "8f14e45f-ceea-467a-9c4a-3c4d1f2f0001",
          "type": "promotion.discount.changed.v1",
          "version": 1,
          "occurredAt": "2026-09-04T12:00:00+00:00",
          "correlationId": "8f14e45f-ceea-467a-9c4a-3c4d1f2f0002",
          "causationId": "8f14e45f-ceea-467a-9c4a-3c4d1f2f0003",
          "productId": "8f14e45f-ceea-467a-9c4a-3c4d1f2f0004",
          "promotionId": "8f14e45f-ceea-467a-9c4a-3c4d1f2f0005",
          "outcome": 0,
          "discountedPriceMinor": 180000,
          "currencyCode": "VND",
          "aFieldPromotionAddedLater": { "nested": true, "count": 7 },
          "anotherNewField": "ignored"
        }
        """;

        var act = () => JsonSerializer.Deserialize<DiscountChangedV1>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var message = act.Should().NotThrow("REL-005 forbids rejecting an unknown field").Subject;
        message!.DiscountedPriceMinor.Should().Be(180_000L, "the known fields still bind");
        message.ProductId.Should().Be(Guid.Parse("8f14e45f-ceea-467a-9c4a-3c4d1f2f0004"));
    }

    [Fact]
    public async Task A_message_with_an_added_field_is_still_applied_to_the_discount_copy()
    {
        await fixture.ResetAsync();
        var product = await fixture.SeedProductAsync(250_000);

        var json = $$"""
        {
          "messageId": "{{Guid.NewGuid()}}",
          "type": "promotion.discount.changed.v1",
          "version": 1,
          "occurredAt": "2026-09-04T12:00:00+00:00",
          "correlationId": "{{Guid.NewGuid()}}",
          "causationId": "{{Guid.NewGuid()}}",
          "productId": "{{product.Id}}",
          "promotionId": "{{Guid.NewGuid()}}",
          "outcome": 0,
          "discountedPriceMinor": 180000,
          "currencyCode": "VND",
          "experimentalTier": "gold"
        }
        """;

        var message = JsonSerializer.Deserialize<DiscountChangedV1>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        await using var db = fixture.NewContext();
        var handler = new Catalog.Infrastructure.Consumers.DiscountChangedHandler(db, fixture.Clock);
        (await handler.HandleAsync(message)).Should().BeTrue();

        await using var check = fixture.NewContext();
        (await check.DiscountProjections.SingleAsync()).DiscountedPrice.AmountMinor
            .Should().Be(180_000L);
    }
}
