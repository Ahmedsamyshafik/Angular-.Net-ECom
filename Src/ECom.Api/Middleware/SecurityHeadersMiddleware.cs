namespace ECom.Api.Middleware;


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