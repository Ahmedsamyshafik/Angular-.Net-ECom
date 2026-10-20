using Ecom.Core.DTO.Products;

namespace Ecom.Core.Services.Orchestrators
{
    public interface IAddProductOrchestrator
    {
        Task<AddProductResult> ExecuteAsync(AddProductDto product, string systemUserId, CancellationToken cancellationToken = default);

        Task CompensateAsync(AddProductResult result, CancellationToken cancellationToken = default);
    }
}
