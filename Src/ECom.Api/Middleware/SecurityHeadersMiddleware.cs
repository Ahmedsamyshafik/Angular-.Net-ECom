namespace ECom.Api.Middleware;

/// <summary>
/// Adds the static security headers that ASP.NET Core does not provide out of the box.
/// Deliberately NOT adding:
///  - Content-Security-Policy: this API returns JSON, not documents, so CSP is inert on
///    API responses, and a policy applied globally would risk breaking browser-served UIs
///    (Scalar in Development, the Hangfire dashboard) which rely on inline scripts/styles.
///  - X-XSS-Protection: obsolete/deprecated, provides no real protection.
///  - HSTS is set separately via the built-in UseHsts() (production only), see Program.cs.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task Invoke(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Prevent MIME-sniffing (browsers must use the declared Content-Type).
        headers["X-Content-Type-Options"] = "nosniff";

        // The API is never meant to be embedded in an iframe.
        headers["X-Frame-Options"] = "DENY";

        // Only send the origin when staying same-origin; strip it on cross-origin requests.
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        return _next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}