using ECommerce.Catalog.Application.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Host.Errors;

/// <summary>
/// FR-029: every rejection carries a reason code the caller can act on. Returning an empty
/// result in place of an error is forbidden, so there is no code path here that produces one.
/// </summary>
public static class CatalogProblemDetails
{
    public static IResult From(string reasonCode, string? detail = null, int? retryAfterSeconds = null)
    {
        var (status, title) = reasonCode switch
        {
            ReasonCodes.MinExceedsMax        => (StatusCodes.Status400BadRequest, "The minimum price exceeds the maximum."),
            ReasonCodes.NegativePriceBound   => (StatusCodes.Status400BadRequest, "A price bound cannot be negative."),
            ReasonCodes.EmptyKeyword         => (StatusCodes.Status400BadRequest, "A search keyword is required."),
            ReasonCodes.ProductNotFound      => (StatusCodes.Status404NotFound,   "Product not found."),
            ReasonCodes.CategoryNotFound     => (StatusCodes.Status404NotFound,   "Category not found."),
            ReasonCodes.RateLimitExceeded    => (StatusCodes.Status429TooManyRequests, "Too many requests."),
            _                                => (StatusCodes.Status400BadRequest, "The request was rejected.")
        };

        var problem = new ProblemDetails
        {
            Title = title,
            Status = status,
            Detail = detail
        };
        problem.Extensions["reasonCode"] = reasonCode;
        if (retryAfterSeconds is not null)
            problem.Extensions["retryAfterSeconds"] = retryAfterSeconds.Value;

        return Results.Problem(
            title: problem.Title,
            detail: problem.Detail,
            statusCode: problem.Status,
            extensions: problem.Extensions);
    }
}
