using Ecom.Core.DTO.CartDto.CartModels;

namespace Ecom.Core.Interfaces
{
    public interface ICartRepository
    {
        Task<Cart?> GetCartAsync(string id, CancellationToken cancellationToken = default);
        Task<bool> SetCartAsync(string id, Cart cart, CancellationToken cancellationToken = default);
        Task<bool> DeleteCartAsync(string id, CancellationToken cancellationToken = default);
    }
}
