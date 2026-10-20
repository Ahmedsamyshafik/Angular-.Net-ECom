using Ecom.Core.Services;
using Ecom.Core.Services.Facads;
using Ecom.Core.Services.Orchestrators;

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

        IOrderService OrderService { get; }

        IShippingAddressesRepo ShippingAddressesRepo { get; }

        IStockReservationService StockReservationService { get; }
        Task Commit(CancellationToken cancellationToken = default);
    }
}
