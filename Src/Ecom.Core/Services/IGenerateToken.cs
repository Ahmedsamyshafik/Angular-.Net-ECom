using Ecom.Core.Entities.User;

namespace Ecom.Core.Services
{
    public interface IGenerateToken
    {
        /// <summary>
        /// Builds the signed JWT for the user. When <paramref name="roles"/> is supplied every
        /// role is emitted as a role claim so role-based authorization (e.g. [Authorize(Roles
        /// = "Admin")]) works against the bearer/cookie token instead of requiring a DB lookup
        /// per request.
        /// </summary>
        string GetAndCreateToken(AppUser user, IEnumerable<string>? roles = null);
    }
}
