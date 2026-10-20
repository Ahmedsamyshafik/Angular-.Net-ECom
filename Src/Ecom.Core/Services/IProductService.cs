using Ecom.Core.DTO.CartDto;
using Ecom.Core.DTO.Products;
using Ecom.Core.Pagination;

namespace Ecom.Core.Services
{
    public interface IProductService
    {
        Task<Pagination<ProductDto>> GetPageAsync(ProductQueryParams query, CancellationToken cancellationToken = default);

        Task<ProductDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        Task<AddProductResult> AddAsync(AddProductDto product, string systemUserId, CancellationToken cancellationToken = default);

        Task CompensateAsync(AddProductResult result, CancellationToken cancellationToken = default);

        Task<ProductDto> UpdateAsync(UpdateProductDto product, string systemUserId, CancellationToken cancellationToken = default);

        Task DeleteAsync(int id, string systemUserId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads the info a cart/order line needs (name, images, current price/discount).
        /// When enforceAvailableStock is true the product's available quantity is validated
        /// against the requested quantity. Order-time reads pass false: by then the quantity
        /// is already reserved and validated against the reservation, not raw stock.
        /// </summary>
        Task<ProductCartInfoDto> GetProductForAddToCartAsync(int productId, int quantity, CancellationToken cancellationToken = default, bool enforceAvailableStock = true);
    }
}
