using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Mvc;

namespace ECom.Api.Registerations
{
    /// <summary>
    /// Registers ASP.NET Core's built-in rate limiter (net10.0) as two independent layers:
    ///   1) GlobalLimiter  -> sliding window, single server-wide partition (truly global).
    ///   2) "per-client"   -> token bucket, partitioned by UserId (authenticated) or IP (anonymous).
    /// Rejection responses are 429 ProblemDetails (same contract as GlobalExceptionHandler)
    /// plus a Retry-After header whenever the limiter exposes retry metadata.
    /// </summary>
    public static class RateLimitingRegisteration
    {
        /// <summary>Key of the single server-wide partition (constant for all clients).</summary>
        public const string GlobalPartitionKey = "global";

        /// <summary>Name of the endpoint policy that carries the per-client token bucket.</summary>
        public const string PerClientPolicyName = "per-client";

        public static IServiceCollection RateLimitingConfigure(this IServiceCollection services, IConfiguration configuration)
        {
            var options = configuration.GetSection("RateLimiting").Get<RateLimitingOptions>()
                          ?? new RateLimitingOptions();

            services.AddRateLimiter(limiter =>
            {
                limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                // Layer 1 - Global sliding window. The partition key is a constant,
                // so every client (authenticated or not) draws from one shared capacity.
                limiter.GlobalLimiter = CreateGlobalLimiter(options);

                // Layer 2 - Per-client token bucket. Partition key comes from
                // GetClientPartitionKey (UserId for authenticated, IP otherwise).
                limiter.AddPolicy<string>(PerClientPolicyName, httpContext =>
                    RateLimitPartition.GetTokenBucketLimiter(
                        GetClientPartitionKey(httpContext),
                        _ => BuildClientOptions(options.Client)));

                limiter.OnRejected = async (context, cancellationToken) =>
                {
                    var httpContext = context.HttpContext;

                    // The limiter tells us when a slot frees up; surface it to the client.
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        httpContext.Response.Headers.RetryAfter =
                            ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(NumberFormatInfo.InvariantInfo);
                    }

                    // Same shape the rest of the API uses for errors: ProblemDetails.
                    var problemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too Many Requests",
                        Detail = "Rate limit exceeded. Please retry later.",
                        Instance = httpContext.Request.Path
                    };
                    problemDetails.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

                    httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    httpContext.Response.ContentType = "application/problem+json";

                    await httpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(problemDetails), cancellationToken);
                };
            });

            return services;
        }

        /// <summary>
        /// Builds the truly global sliding-window limiter. The partition factory ignores
        /// the request and returns the constant GlobalPartitionKey, so all clients share
        /// one window. Used both by the middleware (GlobalLimiter) and directly by tests.
        /// </summary>
        public static PartitionedRateLimiter<HttpContext> CreateGlobalLimiter(RateLimitingOptions options)
        {
            return PartitionedRateLimiter.Create<HttpContext, string>(
                _ => RateLimitPartition.GetSlidingWindowLimiter(
                    GlobalPartitionKey,
                    _ => BuildGlobalOptions(options.Global)));
        }

        /// <summary>
        /// Builds a per-client token-bucket limiter partitioned by UserId (authenticated)
        /// or IP (anonymous). Used by tests to prove per-client isolation deterministically.
        /// </summary>
        public static PartitionedRateLimiter<HttpContext> CreateClientLimiter(RateLimitingOptions options)
        {
            return PartitionedRateLimiter.Create<HttpContext, string>(
                httpContext => RateLimitPartition.GetTokenBucketLimiter(
                    GetClientPartitionKey(httpContext),
                    _ => BuildClientOptions(options.Client)));
        }

        /// <summary>
        /// Per-client partition key:
        ///   authenticated -> "user:{ClaimTypes.NameIdentifier}"
        ///   anonymous     -> "ip:{RemoteIpAddress}" (or "ip:unknown")
        /// IP is intentionally NOT used for authenticated requests.
        /// </summary>
        public static string GetClientPartitionKey(HttpContext httpContext)
        {
            if (httpContext.User.Identity?.IsAuthenticated == true)
            {
                var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    return $"user:{userId}";
                }
            }

            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return $"ip:{ip}";
        }

        private static SlidingWindowRateLimiterOptions BuildGlobalOptions(GlobalRateLimitOptions global)
        {
            return new SlidingWindowRateLimiterOptions
            {
                PermitLimit = global.PermitLimit,
                Window = TimeSpan.FromSeconds(global.WindowSeconds),
                SegmentsPerWindow = global.SegmentsPerWindow,
                QueueLimit = global.QueueLimit,
                AutoReplenishment = true
            };
        }

        private static TokenBucketRateLimiterOptions BuildClientOptions(ClientRateLimitOptions client)
        {
            return new TokenBucketRateLimiterOptions
            {
                TokenLimit = client.TokenLimit,
                TokensPerPeriod = client.TokensPerPeriod,
                ReplenishmentPeriod = TimeSpan.FromSeconds(client.ReplenishmentPeriodSeconds),
                QueueLimit = client.QueueLimit,
                AutoReplenishment = true
            };
        }
    }

    /// <summary>Strongly typed view over the "RateLimiting" configuration section.</summary>
    public sealed class RateLimitingOptions
    {
        public GlobalRateLimitOptions Global { get; set; } = new();
        public ClientRateLimitOptions Client { get; set; } = new();
    }

    public sealed class GlobalRateLimitOptions
    {
        public int PermitLimit { get; set; } = 1000;
        public int WindowSeconds { get; set; } = 60;
        public int SegmentsPerWindow { get; set; } = 4;
        public int QueueLimit { get; set; } = 0;
    }

    public sealed class ClientRateLimitOptions
    {
        public int TokenLimit { get; set; } = 30;
        public int TokensPerPeriod { get; set; } = 30;
        public int ReplenishmentPeriodSeconds { get; set; } = 60;
        public int QueueLimit { get; set; } = 0;
    }
}