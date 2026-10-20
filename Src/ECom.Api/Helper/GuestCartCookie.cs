using Microsoft.AspNetCore.Http;

namespace ECom.Api.Helper
{
    /// <summary>
    /// Owns the browser cookie that carries the guest cart id. The id is issued by the server
    /// on the first anonymous cart call and reused for every following request, so a guest's
    /// basket survives across requests. The cookie is readable by the SPA (not HttpOnly) so the
    /// login request can send it as loginDto.cartGuestId and the API merges the guest basket
    /// into the authenticated user's basket.
    /// </summary>
    public static class GuestCartCookie
    {
        public const string Name = "cartGuestId";

        /// <summary>
        /// Returns the guest cart id for the current request: the existing cookie value when
        /// present, otherwise a new id that is also written to the response so the browser
        /// sends it back on the next request.
        /// </summary>
        public static string Resolve(HttpContext httpContext)
        {
            if (httpContext.Request.Cookies.TryGetValue(Name, out var existing)
                && !string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }

            var id = Guid.NewGuid().ToString();
            httpContext.Response.Cookies.Append(Name, id, CreateOptions(httpContext));
            return id;
        }

        /// <summary>Expires the guest-cart cookie after a successful login merge.</summary>
        public static void Clear(HttpContext httpContext)
        {
            httpContext.Response.Cookies.Delete(Name, CreateOptions(httpContext));
        }

        private static CookieOptions CreateOptions(HttpContext httpContext) => new()
        {
            HttpOnly = false, // the SPA must read it to send cartGuestId during login
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        };
    }
}
