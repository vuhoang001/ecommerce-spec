using System.Net;
using System.Text.Json;
using ECommerce.Catalog.Application.Contracts;
using FluentAssertions;

namespace ECommerce.Catalog.ResilienceTests;

/// <summary>
/// FR-035, FR-037, SC-014 — an over-limit caller is refused with a reason code and a
/// Retry-After, never a short page or an empty list.
/// </summary>
[Collection("resilience")]
public class RateLimitTests(ResilienceFixture fixture)
{
    private const string SomeCategory = "/catalog/categories/11111111-1111-1111-1111-111111111111/products";

    [Fact]
    public async Task An_over_limit_caller_is_refused_with_a_reason_and_a_retry_after()
    {
        var client = fixture.CreateClient();

        HttpResponseMessage? refused = null;
        for (var i = 0; i < ResilienceFixture.TokensPerMinute + 3; i++)
        {
            var response = await client.GetAsync(SomeCategory);
            if (response.StatusCode == HttpStatusCode.TooManyRequests) { refused = response; break; }
        }

        refused.Should().NotBeNull("the budget is {0} per minute", ResilienceFixture.TokensPerMinute);
        refused!.Headers.RetryAfter.Should().NotBeNull("FR-035 requires stating when to retry");

        using var document = JsonDocument.Parse(await refused.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("reasonCode").GetString()
            .Should().Be(ReasonCodes.RateLimitExceeded);
        document.RootElement.GetProperty("retryAfterSeconds").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task A_refusal_is_never_a_silently_empty_or_truncated_result()
    {
        var client = fixture.CreateClient();

        for (var i = 0; i < ResilienceFixture.TokensPerMinute + 3; i++)
        {
            var response = await client.GetAsync(SomeCategory);
            if (response.StatusCode != HttpStatusCode.TooManyRequests) continue;

            var body = await response.Content.ReadAsStringAsync();
            body.Should().NotContain("\"items\"", "SC-014 forbids answering a refusal with a result shape");
            return;
        }

        Assert.Fail("The limiter never refused; the budget is not being applied.");
    }

    [Fact]
    public async Task The_limiter_runs_ahead_of_the_handler_so_no_visibility_check_is_skipped()
    {
        // FR-037: a refused caller never reaches a query, so there is no path where load
        // causes a non-Active product to be returned.
        var client = fixture.CreateClient();

        var refusedBeforeReachingTheHandler = false;
        for (var i = 0; i < ResilienceFixture.TokensPerMinute + 3; i++)
        {
            var response = await client.GetAsync("/catalog/categories/" + Guid.NewGuid() + "/products");
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                // A 429 rather than the 404 the handler would have produced proves the
                // limiter short-circuits before the query runs.
                refusedBeforeReachingTheHandler = true;
                break;
            }
        }

        refusedBeforeReachingTheHandler.Should().BeTrue();
    }

    [Fact]
    public async Task The_aggregate_limit_across_instances_is_recorded_not_asserted_as_exact()
    {
        // research.md R11: the budget is held PER INSTANCE, so N instances admit roughly N times
        // the per-instance budget under uneven balancing. This test documents that imprecision
        // rather than pretending the limit is exact — making it exact needs a shared counter,
        // which means Redis, which STK-001 does not permit without a GOV-002 amendment.
        var instanceA = fixture.CreateClient();
        var instanceB = fixture.CreateClient();

        var admitted = 0;
        for (var i = 0; i < ResilienceFixture.TokensPerMinute * 4; i++)
        {
            var client = i % 2 == 0 ? instanceA : instanceB;
            var response = await client.GetAsync(SomeCategory);
            if (response.StatusCode != HttpStatusCode.TooManyRequests) admitted++;
        }

        // A single shared budget would admit exactly TokensPerMinute. The assertion is a bound,
        // not an equality, because the guarantee itself is a bound.
        admitted.Should().BeLessThanOrEqualTo(ResilienceFixture.TokensPerMinute * 2,
            "the per-instance budget is the total divided by the instance count; the aggregate " +
            "is bounded by instances x per-instance budget, never unbounded");
    }
}
