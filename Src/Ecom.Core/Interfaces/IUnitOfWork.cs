using Ecom.Core.Services;

namespace Ecom.Core.Interfaces
{
    public interface IUnitOfWork
    {
        IProductService ProductService { get; }

        ICategoryService CategoryService { get; }

        IDiscountFacade DiscountFacade { get; }

        IAddProductOrchestrator AddProductOrchestrator { get; }

        ICartProductFacade CartProductFacade { get; }
        IMyAuth myAuth { get; }

        Task Commit(CancellationToken cancellationToken = default);
    }
}
