using System.Net;
using System.Net.Http.Headers;
using ECom.Api.Middleware;
using ECom.Api.Registerations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ECom.Api.Tests.SecurityHeaders;

/// <summary>
/// Verifies the centralized security headers (ECom.Api.Middleware.SecurityHeadersMiddleware)
/// and the built-in HSTS wiring used by Program.cs, end-to-end over an in-process Kestrel app.
/// CSP is deliberately not applied (JSON API + Scalar/Hangfire UIs) — asserted absent.
/// </summary>
public class SecurityHeadersTests
{
    // ---------- Static headers ----------

    [Fact]
    public async Task ApiResponses_CarryTheSecurityHeaders()
    {
        var app = await StartProbeAppAsync(configureApp: app =>
        {
            app.UseSecurityHeaders();
            app.MapGet("/probe", () => Results.Ok("ok"));
        });

        try
        {
            var response = await app.Client.GetAsync("/probe");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
            Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
        }
        finally
        {
            await app.App.DisposeAsync();
        }
    }

    [Fact]
    public async Task SecurityHeaders_AreAlsoSentOnRateLimited429Responses()
    {
        var app = await StartProbeAppAsync(configureBuilder: builder =>
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Global:PermitLimit"] = "1000",
                ["RateLimiting:Global:WindowSeconds"] = "60",
                ["RateLimiting:Global:SegmentsPerWindow"] = "4",
                ["RateLimiting:Global:QueueLimit"] = "0",
                ["RateLimiting:Client:TokenLimit"] = "2",
                ["RateLimiting:Client:TokensPerPeriod"] = "2",
                ["RateLimiting:Client:ReplenishmentPeriodSeconds"] = "60",
                ["RateLimiting:Client:QueueLimit"] = "0"
            });
            builder.Services.RateLimitingConfigure(builder.Configuration);
        }, configureApp: app =>
        {
            // Security headers must run BEFORE the rate limiter so its 429 short-circuit
            // still carries them (mirrors Program.cs ordering).
            app.UseSecurityHeaders();
            app.UseRateLimiter();
            app.MapGet("/probe", () => Results.Ok("ok"))
               .RequireRateLimiting(RateLimitingRegisteration.PerClientPolicyName);
        });

        try
        {
            Assert.Equal(HttpStatusCode.OK, (await app.Client.GetAsync("/probe")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await app.Client.GetAsync("/probe")).StatusCode);
            var rejected = await app.Client.GetAsync("/probe");

            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
            Assert.Equal("nosniff", rejected.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("DENY", rejected.Headers.GetValues("X-Frame-Options").Single());
            Assert.Equal("strict-origin-when-cross-origin", rejected.Headers.GetValues("Referrer-Policy").Single());
        }
        finally
        {
            await app.App.DisposeAsync();
        }
    }

    // ---------- HSTS (built-in UseHsts) ----------

    [Fact]
    public async Task Hsts_IsSentInProductionLikeWiring()
    {
        // Mirrors Program.cs: UseHsts() is only invoked when the environment is NOT Development.
        // net10's HstsMiddleware only emits the header over HTTPS and skips excluded hosts
        // (defaults: localhost / 127.0.0.1 / [::1]). To exercise the real middleware the probe
        // simulates a genuine HTTPS request: scheme forced to https + a non-excluded Host.
        var app = await StartProbeAppAsync(configureApp: app =>
        {
            app.Environment.EnvironmentName = Environments.Production;
            app.Use(async (context, next) =>
            {
                context.Request.Scheme = Uri.UriSchemeHttps;
                await next();
            });
            app.UseHsts();
            app.MapGet("/probe", () => Results.Ok("ok"));
        });

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
            request.Headers.Host = "api.ecom.test.example";

            var response = await app.Client.SendAsync(request);

            Assert.True(response.Headers.TryGetValues("Strict-Transport-Security", out var values), "HSTS missing on HTTPS request");
            Assert.Contains("max-age=", values!.Single());
        }
        finally
        {
            await app.App.DisposeAsync();
        }
    }

    [Fact]
    public async Task Hsts_IsNotSentInDevelopmentLikeWiring()
    {
        // In Development, Program.cs skips UseHsts() entirely. The same HTTPS simulation is
        // used as in the production test: the omission is caused by wiring, not by transport
        // or excluded hosts.
        var app = await StartProbeAppAsync(configureApp: app =>
        {
            app.Environment.EnvironmentName = Environments.Development;
            app.Use(async (context, next) =>
            {
                context.Request.Scheme = Uri.UriSchemeHttps;
                await next();
            });
            app.MapGet("/probe", () => Results.Ok("ok"));
        });

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
            request.Headers.Host = "api.ecom.test.example";

            var response = await app.Client.SendAsync(request);
            Assert.False(response.Headers.Contains("Strict-Transport-Security"));
        }
        finally
        {
            await app.App.DisposeAsync();
        }
    }

    // ---------- Deliberately-not-applied headers ----------

    [Fact]
    public async Task Csp_IsDeliberatelyNotSent()
    {
        var app = await StartProbeAppAsync(configureApp: app =>
        {
            app.UseSecurityHeaders();
            app.MapGet("/probe", () => Results.Ok("ok"));
        });

        try
        {
            var response = await app.Client.GetAsync("/probe");
            Assert.False(response.Headers.Contains("Content-Security-Policy"));
        }
        finally
        {
            await app.App.DisposeAsync();
        }
    }

    [Fact]
    public async Task ObsoleteXssProtectionHeader_IsNotSent()
    {
        var app = await StartProbeAppAsync(configureApp: app =>
        {
            app.UseSecurityHeaders();
            app.MapGet("/probe", () => Results.Ok("ok"));
        });

        try
        {
            var response = await app.Client.GetAsync("/probe");
            Assert.False(response.Headers.Contains("X-XSS-Protection"));
        }
        finally
        {
            await app.App.DisposeAsync();
        }
    }

    // ---------- Helper ----------

    private static async Task<(WebApplication App, HttpClient Client)> StartProbeAppAsync(
        Action<WebApplicationBuilder>? configureBuilder = null,
        Action<WebApplication>? configureApp = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        configureBuilder?.Invoke(builder);

        var app = builder.Build();
        app.Environment.EnvironmentName = Environments.Production;
        configureApp?.Invoke(app);

        await app.StartAsync();

        var addresses = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses;

        var client = new HttpClient { BaseAddress = new Uri(addresses.First()) };
        return (app, client);
    }
}