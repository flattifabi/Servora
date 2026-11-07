using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Servora.Application.Api;

namespace Servora.Web.Api;

/// <summary>
/// Global rate limiting of the API surface: requests with an API key are limited per key (the key's configured limit,
/// looked up by key id because the limiter runs before authentication); other API requests are limited per client IP.
/// </summary>
public static class ApiRateLimiting
{
    /// <summary>The per-IP limit for API requests without a key (requests per minute).</summary>
    public const int AnonymousLimitPerMinute = 300;

    /// <summary>Configures the global limiter and the rejection response.</summary>
    /// <param name="options">The rate limiter options.</param>
    public static void Configure(RateLimiterOptions options)
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (!ApiCredentialReader.IsApiPath(context.Request.Path))
            {
                return RateLimitPartition.GetNoLimiter("ui");
            }

            // Only keys that authenticated successfully before get their own partition; unknown (possibly random) key ids
            // stay in the client address partition so that rotating fake ids cannot bypass the limit.
            if (ApiKeyTokens.TryGetKeyId(ApiCredentialReader.ReadToken(context.Request)) is { } keyId
                && context.RequestServices.GetRequiredService<ApiKeyRateLimitRegistry>().Get(keyId) is { } limit)
            {
                return RateLimitPartition.GetFixedWindowLimiter($"key:{keyId}:{limit}", _ => Window(limit));
            }

            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter($"ip:{ip}", _ => Window(AnonymousLimitPerMinute));
        });

        options.OnRejected = async (context, cancellationToken) =>
        {
            var http = context.HttpContext;
            http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                http.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            }

            if (ApiCredentialReader.IsApiPath(http.Request.Path))
            {
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests",
                    Detail = "The rate limit of this API key or client was exceeded. Retry after the time given in the Retry-After header.",
                };
                var service = http.RequestServices.GetService<IProblemDetailsService>();
                if (service is null || !await service.TryWriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem }))
                {
                    await http.Response.WriteAsJsonAsync(problem, cancellationToken);
                }
            }
        };
    }

    private static FixedWindowRateLimiterOptions Window(int permitLimit) => new()
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
        AutoReplenishment = true,
    };
}
