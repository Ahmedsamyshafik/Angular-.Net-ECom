using ECom.Api.Helper;

using Microsoft.AspNetCore.Http;

namespace ECom.Api.Tests.Helper;

/// <summary>
/// The guest cart id must be stable across requests (issued once, then read back) because the
/// previous implementation generated a new GUID on every request, which made guest carts
/// impossible to retrieve. The cookie also has to be readable by the SPA so the login request
/// can forward it as loginDto.cartGuestId.
/// </summary>
public class GuestCartCookieTests
{
    [Fact]
    public void Resolve_WithNoCookie_IssuesNewIdAndWritesNonHttpOnlyCookie()
    {
        var context = new DefaultHttpContext();

        var id = GuestCartCookie.Resolve(context);

        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.True(Guid.TryParse(id, out _));

        var setCookie = context.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains($"{GuestCartCookie.Name}={id}", setCookie);
        Assert.DoesNotContain("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_WithExistingCookie_ReusesItAndDoesNotWriteAnother()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{GuestCartCookie.Name}=existing-guest-id";

        var id = GuestCartCookie.Resolve(context);

        Assert.Equal("existing-guest-id", id);
        Assert.True(string.IsNullOrEmpty(context.Response.Headers["Set-Cookie"].ToString()));
    }

    [Fact]
    public void Clear_ExpiresTheCookie()
    {
        var context = new DefaultHttpContext();

        GuestCartCookie.Clear(context);

        var setCookie = context.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains(GuestCartCookie.Name, setCookie);
    }
}
