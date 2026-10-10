using Ecom.Core.Entities.User;

namespace Ecom.Core.Services
{
    public interface IGenerateToken
    {
        string GetAndCreateToken(AppUser user);
    }
}
