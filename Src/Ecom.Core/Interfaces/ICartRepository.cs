using Ecom.Core.CartModels;

namespace Ecom.Core.Interfaces
{
    public interface ICartRepository
    {
        Task<Cart?> GetCartAsync(string id, CancellationToken cancellationToken);
        Task<bool> SetCartAsync(string id, Cart cart);
        Task<bool> DeleteCartAsync(string id);
    }
}
