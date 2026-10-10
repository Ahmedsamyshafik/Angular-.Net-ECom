using System.Net;
using System.Security.Claims;
using System.Text.Json;
using ECom.Api.Registerations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ECom.Api.Tests.RateLimiting;

/// <summary>
/// Tests for the built-in rate limiting (ECom.Api.Registerations).
/// Strategy: partition-level unit tests exercise the exact same limiter factories the
/// middleware uses (CreateGlobalLimiter / CreateClientLimiter), so per-IP and per-UserId
/// behavior is deterministic and does not depend on real-time waits. A small in-process
/// Kestrel app proves the real 429 ProblemDetails + Retry-After path end-to-end.
/// </summary>
public class RateLimitingTests
{
    // ---------- Global sliding window ----------

    [Fact]
    public async Task GlobalLimiter_IsTrulyGlobal_DifferentClientsShareOneCapacity()
    {
        var options = new RateLimitingOptions();
        options.Global.PermitLimit = 2;
        options.Global.WindowSeconds = 60;
        options.Global.SegmentsPerWindow = 4;
        options.Global.QueueLimit = 0;

        using var limiter = RateLimitingRegisteration.CreateGlobalLimiter(options);

        var anonymousIp1 = ContextWith("10.0.0.1");
        var authenticatedIp2 = ContextWith("10.0.0.2", "user-2");
        var anonymousIp3 = ContextWith("10.0.0.3");

        var first = await limiter.AcquireAsync(anonymousIp1, 1, CancellationToken.None);
        Assert.True(first.IsAcquired);

        var second = await limiter.AcquireAsync(authenticatedIp2, 1, CancellationToken.None);
        Assert.True(second.IsAcquired);

        // A third, unrelated client is rejected because the capacity is server-wide,
        // not per-client. (No replenishment happens within a test window.)
        var third = await limiter.AcquireAsync(anonymousIp3, 1, CancellationToken.None);
        Assert.False(third.IsAcquired);
    }

    // ---------- Anonymous token bucket (per IP) ----------

    [Fact]
    public async Task ClientLimiter_AnonymousRequests_ArePartitionedByIp()
    {
        var options = new RateLimitingOptions();
        options.Client.TokenLimit = 2;
        options.Client.TokensPerPeriod = 2;
        options.Client.ReplenishmentPeriodSeconds = 60;
        options.Client.QueueLimit = 0;

        using var limiter = RateLimitingRegisteration.CreateClientLimiter(options);

        var ip1a = ContextWith("10.0.0.1");
        var ip1b = ContextWith("10.0.0.1");
        var ip2 = ContextWith("10.0.0.2");

        Assert.True((await limiter.AcquireAsync(ip1a, 1, CancellationToken.None)).IsAcquired);
        Assert.True((await limiter.AcquireAsync(ip1b, 1, CancellationToken.None)).IsAcquired);
        // Same IP has consumed its bucket (2 tokens), third request denied.
        Assert.False((await limiter.AcquireAsync(ip1b, 1, CancellationToken.None)).IsAcquired);
        // A different IP has an independent, full bucket.
        Assert.True((await limiter.AcquireAsync(ip2, 1, CancellationToken.None)).IsAcquired);
    }

    // ---------- Authenticated token bucket (per UserId) ----------

    [Fact]
    public async Task ClientLimiter_AuthenticatedRequests_ArePartitionedByUserId_NotIp()
    {
        var options = new RateLimitingOptions();
        options.Client.TokenLimit = 1;
        options.Client.TokensPerPeriod = 1;
        options.Client.ReplenishmentPeriodSeconds = 60;
        options.Client.QueueLimit = 0;

        using var limiter = RateLimitingRegisteration.CreateClientLimiter(options);

        // Two different users behind the SAME ip address must get independent buckets.
        var userA = ContextWith("10.0.0.1", "user-A");
        var userB = ContextWith("10.0.0.1", "user-B");

        Assert.True((await limiter.AcquireAsync(userA, 1, CancellationToken.None)).IsAcquired);
        // user-B still has a full bucket even though it shares the IP with user-A.
        Assert.True((await limiter.AcquireAsync(userB, 1, CancellationToken.None)).IsAcquired);
        // Repeating user-A now drains the already-consumed bucket.
        Assert.False((await limiter.AcquireAsync(userA, 1, CancellationToken.None)).IsAcquired);
    }

    [Fact]
    public async Task ClientLimiter_AuthenticatedAndAnonymous_OnSameIp_HaveIndependentBuckets()
    {
        var options = new RateLimitingOptions();
        options.Client.TokenLimit = 1;
        options.Client.TokensPerPeriod = 1;
        options.Client.ReplenishmentPeriodSeconds = 60;
        options.Client.QueueLimit = 0;

        using var limiter = RateLimitingRegisteration.CreateClientLimiter(options);

        var authenticated = ContextWith("10.0.0.1", "user-9");
        var anonymous = ContextWith("10.0.0.1");

        Assert.True((await limiter.AcquireAsync(authenticated, 1, CancellationToken.None)).IsAcquired);
        // Anonymous request from the same IP is NOT keyed by IP when auth is present,
        // but here the anonymous user is keyed by IP -> separate bucket.
        Assert.True((await limiter.AcquireAsync(anonymous, 1, CancellationToken.None)).IsAcquired);
        Assert.False((await limiter.AcquireAsync(authenticated, 1, CancellationToken.None)).IsAcquired);
    }

    // ---------- Partition key logic ----------

    [Fact]
    public void GetClientPartitionKey_UsesUserIdWhenAuthenticatedAndIpOtherwise()
    {
        Assert.Equal("user:user-A", RateLimitingRegisteration.GetClientPartitionKey(ContextWith("10.0.0.1", "user-A")));
        Assert.Equal("ip:10.0.0.1", RateLimitingRegisteration.GetClientPartitionKey(ContextWith("10.0.0.1")));
        Assert.Equal("ip:unknown", RateLimitingRegisteration.GetClientPartitionKey(ContextWith(null)));
    }

    [Fact]
    public void GetClientPartitionKey_AuthenticatedWithoutUserIdClaim_FallsBackToIp()
    {
        var noUserIdClaim = new DefaultHttpContext();
        Assert.Equal("ip:unknown", RateLimitingRegisteration.GetClientPartitionKey(noUserIdClaim));
    }

    // ---------- End-to-end: real middleware, 429, ProblemDetails, Retry-After ----------

    [Fact]
    public async Task GlobalSlidingWindow_WhenExhausted_Returns429ProblemDetails()
    {
        var (app, client) = await StartProbeAppAsync(o =>
        {
            o.Global.PermitLimit = 2;
            o.Global.WindowSeconds = 60;
            o.Global.SegmentsPerWindow = 4;
            o.Global.QueueLimit = 0;
            o.Client.TokenLimit = 1000;
            o.Client.TokensPerPeriod = 1000;
            o.Client.ReplenishmentPeriodSeconds = 60;
            o.Client.QueueLimit = 0;
        });

        try
        {
            var first = await client.GetAsync("/probe");
            var second = await client.GetAsync("/probe");
            var rejected = await client.GetAsync("/probe");

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
            Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);

            var body = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
            Assert.Equal(429, body.RootElement.GetProperty("status").GetInt32());
            Assert.Equal("Too Many Requests", body.RootElement.GetProperty("title").GetString());
        }
        finally
        {
            client.Dispose();
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task ClientTokenBucket_WhenExhausted_Returns429WithRetryAfter()
    {
        var (app, client) = await StartProbeAppAsync(o =>
        {
            o.Global.PermitLimit = 1000;
            o.Global.WindowSeconds = 60;
            o.Global.SegmentsPerWindow = 4;
            o.Global.QueueLimit = 0;
            o.Client.TokenLimit = 2;
            o.Client.TokensPerPeriod = 2;
            o.Client.ReplenishmentPeriodSeconds = 60;
            o.Client.QueueLimit = 0;
        });

        try
        {
            var first = await client.GetAsync("/probe");
            var second = await client.GetAsync("/probe");
            var rejected = await client.GetAsync("/probe");

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
            Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);

            Assert.NotNull(rejected.Headers.RetryAfter);
            Assert.True(int.TryParse(rejected.Headers.RetryAfter.ToString(), out var seconds) && seconds > 0);

            var body = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
            Assert.Equal(429, body.RootElement.GetProperty("status").GetInt32());
        }
        finally
        {
            client.Dispose();
            await app.DisposeAsync();
        }
    }

    // ---------- Helpers ----------

    private static HttpContext ContextWith(string? ip, string? userId = null)
    {
        var context = new DefaultHttpContext();

        if (ip is not null)
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        }

        if (userId is not null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId) },
                authenticationType: "RateLimitingTest"));
        }

        return context;
    }

    /// <summary>
    /// Boots a tiny Kestrel app that runs the SAME production rate-limiter registration
    /// (RateLimitingConfigure) and a single /probe endpoint opted into the per-client
    /// policy. No SQL / Hangfire involved, so it is fast and self-contained.
    /// </summary>
    private static async Task<(WebApplication App, HttpClient Client)> StartProbeAppAsync(
        Action<RateLimitingOptions> configure)
    {
        var options = new RateLimitingOptions();
        configure(options);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RateLimiting:Global:PermitLimit"] = options.Global.PermitLimit.ToString(),
            ["RateLimiting:Global:WindowSeconds"] = options.Global.WindowSeconds.ToString(),
            ["RateLimiting:Global:SegmentsPerWindow"] = options.Global.SegmentsPerWindow.ToString(),
            ["RateLimiting:Global:QueueLimit"] = options.Global.QueueLimit.ToString(),
            ["RateLimiting:Client:TokenLimit"] = options.Client.TokenLimit.ToString(),
            ["RateLimiting:Client:TokensPerPeriod"] = options.Client.TokensPerPeriod.ToString(),
            ["RateLimiting:Client:ReplenishmentPeriodSeconds"] = options.Client.ReplenishmentPeriodSeconds.ToString(),
            ["RateLimiting:Client:QueueLimit"] = options.Client.QueueLimit.ToString()
        });

        builder.Services.RateLimitingConfigure(builder.Configuration);

        var app = builder.Build();
        app.UseRateLimiter();
        app.MapGet("/probe", () => Results.Ok("ok"))
           .RequireRateLimiting(RateLimitingRegisteration.PerClientPolicyName);

        await app.StartAsync();

        var addresses = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses;

        var client = new HttpClient { BaseAddress = new Uri(addresses.First()) };
        return (app, client);
    }
}