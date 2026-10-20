using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Ecom.Core.Entities.User;
using Ecom.Core.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ECom.Infrastructure.Services
{
    public class GenereateToken : IGenerateToken
    {
        private readonly IConfiguration _configuration;
        public GenereateToken(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public string GetAndCreateToken(AppUser user, IEnumerable<string>? roles = null)
        {
            string secuirty = _configuration["token:Secret"];
            var key = Encoding.ASCII.GetBytes(secuirty);

            SigningCredentials credentials =
                new(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);
            List<Claim> claims = new()
            {
                new Claim(ClaimTypes.Name,user.UserName),
                new Claim(ClaimTypes.Email,user.Email),
                new Claim(ClaimTypes.NameIdentifier,user.Id)
            };

            // Role claims drive [Authorize(Roles = AppRoles.Admin)] on the catalog writes.
            if (roles is not null)
            {
                foreach (var role in roles)
                {
                    if (!string.IsNullOrWhiteSpace(role))
                    {
                        claims.Add(new Claim(ClaimTypes.Role, role));
                    }
                }
            }
            SecurityTokenDescriptor tokenDescriptor = new()
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.Now.AddDays(1),
                Issuer = _configuration["token:Issure"],
                SigningCredentials = credentials,
                NotBefore=DateTime.Now
            };
            JwtSecurityTokenHandler handler = new();
            var token = handler.CreateToken(tokenDescriptor);


            return handler.WriteToken(token);
        }
    }
}
