using System.Threading.RateLimiting;
using ECommerce.Catalog.Application.Contracts;
using ECommerce.Host.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Host.RateLimiting;

/// <summary>
/// FR-035 / FR-037 — a per-caller token bucket, held in this instance's memory.
/// </summary>
/// <remarks>
/// research.md R11: the budget is the total divided by the instance count, so the limit is
/// APPROXIMATE across a redundant deployment. Making it exact needs a shared counter, which
/// means Redis, which STK-001 does not permit without a GOV-002 amendment. The limit exists
/// to stop a scraper pulling 100,000 products, not to meter a paid quota, so being off by a
/// factor of the instance count under uneven balancing does not defeat its purpose.
/// </remarks>
public static class CatalogRateLimiter
{
    public const string PolicyName = "catalog-read";

    public static IServiceCollection AddCatalogRateLimiting(
        this IServiceCollection services, IConfiguration configuration)
    {
        var totalPerMinute = configuration.GetValue("RateLimit:TotalTokensPerMinute", 600);
        var instanceCount = Math.Max(1, configuration.GetValue("RateLimit:InstanceCount", 1));
        var perInstance = Math.Max(1, totalPerMinute / instanceCount);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(PolicyName, httpContext =>
                RateLimitPartition.GetTokenBucketLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = perInstance,
                        TokensPerPeriod = perInstance,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    }));

            // FR-035 / SC-014: the rejection states the reason and when to retry.
            // It is never a silently empty or truncated result.
            options.OnRejected = async (context, cancellationToken) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var value)
                    ? (int)Math.Ceiling(value.TotalSeconds)
                    : 60;

                context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString();

                await CatalogProblemDetails
                    .From(ReasonCodes.RateLimitExceeded,
                          $"Rate limit exceeded. Retry in {retryAfter} seconds.",
                          retryAfter)
                    .ExecuteAsync(context.HttpContext);
            };
        });

        return services;
    }
}
