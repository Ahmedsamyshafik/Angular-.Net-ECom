using Ecom.Core.CartModels;
using Ecom.Core.DTO.CartDto;

namespace Ecom.Core.Services
{
    public interface ICartProductFacade
    {
        Task<Cart> GetCart(string userId, CancellationToken cancellationToken);
        Task<Cart> AddProductToCartAsync(string userId, int productId, int quantity, CancellationToken cancellationToken);
        Task<Cart> UpdateCartItemAsync(
         string userId,
         int productId,
         int quantity,
         CancellationToken cancellationToken);
        Task DeleteCartAsync(string userId);
        Task<Cart> UpdateCartAsync(UpdateCartRequest request, CancellationToken cancellationToken);
    }
}
