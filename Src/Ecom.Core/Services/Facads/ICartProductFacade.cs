using Ecom.Core.DTO.CartDto;
using Ecom.Core.DTO.CartDto.CartModels;

namespace Ecom.Core.Services.Facads
{
    public interface ICartProductFacade
    {
        Task<Cart> GetCart(string userId, CancellationToken cancellationToken = default);
        Task<Cart> AddProductToCartAsync(string userId, int productId, int quantity, CancellationToken cancellationToken = default);
        Task<Cart> UpdateCartItemAsync(
         string userId,
         int productId,
         int quantity,
         CancellationToken cancellationToken = default);
        Task DeleteCartAsync(string userId, CancellationToken cancellationToken = default);
        Task<Cart> UpdateCartAsync(UpdateCartRequest request, CancellationToken cancellationToken = default);
        Task UpdateCartIdForGuestUsers(string CartGuestId, string userId, CancellationToken cancellation = default);
    }
}
