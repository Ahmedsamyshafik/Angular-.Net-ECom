using Ecom.Core.DTO.CartDto;
using Ecom.Core.DTO.Products;
using Ecom.Core.Pagination;

namespace Ecom.Core.Services
{
    public interface IProductService
    {
        Task<Pagination<ProductDto>> GetPageAsync(ProductQueryParams query, CancellationToken cancellationToken = default);

        Task<ProductDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        Task<AddProductResult> AddAsync(AddProductDto product, CancellationToken cancellationToken = default);

        Task CompensateAsync(AddProductResult result, CancellationToken cancellationToken = default);

        Task<ProductDto> UpdateAsync(UpdateProductDto product, CancellationToken cancellationToken = default);

        Task DeleteAsync(int id, CancellationToken cancellationToken = default);
        Task<ProductCartInfoDto> GetProductForAddToCartAsync(int productId, int quantity, CancellationToken cancellationToken = default);
    }
}
