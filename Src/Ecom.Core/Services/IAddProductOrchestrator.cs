using Ecom.Core.DTO.Products;

namespace Ecom.Core.Services
{
    public interface IAddProductOrchestrator
    {
        Task<AddProductResult> ExecuteAsync(AddProductDto product, CancellationToken cancellationToken = default);

        Task CompensateAsync(AddProductResult result, CancellationToken cancellationToken = default);
    }
}
