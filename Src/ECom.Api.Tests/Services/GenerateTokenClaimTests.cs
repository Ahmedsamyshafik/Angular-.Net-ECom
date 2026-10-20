using System.IdentityModel.Tokens.Jwt;

using Ecom.Core.Entities.User;
using Ecom.Core.Services;

using ECom.Infrastructure.Services;

using Microsoft.Extensions.Configuration;

namespace ECom.Api.Tests.Services;

public class GenerateTokenClaimTests
{
    [Fact]
    public void GetAndCreateToken_EmitsNameIdentifierEmailAndNameClaims()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["token:Secret"] = "test-secret-key-that-is-at-least-32-characters-long!",
                ["token:Issure"] = "test-issuer"
            })
            .Build();

        var generator = new GenereateToken(configuration);
        var token = generator.GetAndCreateToken(new AppUser
        {
            Id = "user-123",
            UserName = "buyer",
            Email = "buyer@example.com"
        });

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        string valueOf(string type) =>
            jwt.Claims.Single(c => c.Type == type).Value;

        // JwtSecurityTokenHandler writes the JWT short names that ASP.NET Core's inbound
        // claim map reconstructs into ClaimTypes.* (nameidentifier/name/email) at request time.
        Assert.Equal("user-123", valueOf("nameid"));
        Assert.Equal("buyer", valueOf("unique_name"));
        Assert.Equal("buyer@example.com", valueOf("email"));
    }

    [Fact]
    public void GetAndCreateToken_WithRoles_EmitsRoleClaims()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["token:Secret"] = "test-secret-key-that-is-at-least-32-characters-long!",
                ["token:Issure"] = "test-issuer"
            })
            .Build();

        var generator = new GenereateToken(configuration);
        var token = generator.GetAndCreateToken(
            new AppUser
            {
                Id = "admin-1",
                UserName = "admin",
                Email = "admin@example.com"
            },
            new[] { "Admin" });

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        // "role" is the short wire name JwtSecurityTokenHandler writes for ClaimTypes.Role;
        // ASP.NET Core's inbound claim map turns it back into ClaimTypes.Role, which is what
        // [Authorize(Roles = "Admin")] checks.
        Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == "Admin");
    }

    [Fact]
    public void GetAndCreateToken_WithoutRoles_EmitsNoRoleClaim()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["token:Secret"] = "test-secret-key-that-is-at-least-32-characters-long!",
                ["token:Issure"] = "test-issuer"
            })
            .Build();

        var generator = new GenereateToken(configuration);
        var token = generator.GetAndCreateToken(new AppUser { Id = "user-123", UserName = "buyer", Email = "buyer@example.com" });

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.DoesNotContain(jwt.Claims, c => c.Type == "role");
    }
}