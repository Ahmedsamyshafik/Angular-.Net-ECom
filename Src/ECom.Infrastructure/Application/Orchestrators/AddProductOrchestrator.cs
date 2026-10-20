using Ecom.Core.DTO.Products;
using Ecom.Core.Services;
using Ecom.Core.Services.Facads;
using Ecom.Core.Services.Orchestrators;

namespace ECom.Infrastructure.Application.Orchestrators
{
    public class AddProductOrchestrator : IAddProductOrchestrator
    {
        #region Private Fields

        private readonly IProductService _productService;
        private readonly IDiscountFacade _discountFacade;

        #endregion

        #region Constructors

        public AddProductOrchestrator(IProductService productService, IDiscountFacade discountFacade)
        {
            _productService = productService;
            _discountFacade = discountFacade;
        }

        #endregion

        #region Methods

        public async Task<AddProductResult> ExecuteAsync(
            AddProductDto product,
            string systemUserId,
            CancellationToken cancellationToken = default)
        {
            var result = await _productService.AddAsync(product, systemUserId, cancellationToken);

            try
            {
                if (product.Discount is not null)
                {
                    if (result.Entity is not null)
                    {
                        // Preferred path: link through the tracked entity so EF resolves the
                        // generated product id (the DTO id is still 0 until SaveChanges).
                        await _discountFacade.ApplyDiscountToTrackedProductAsync(
                            product.Discount,
                            result.Entity,
                            cancellationToken);
                    }
                    else
                    {
                        product.Discount.ProductIds = new List<int> { result.Product.Id };
                        await _discountFacade.ApplyDiscountAsync(product.Discount, cancellationToken);
                    }
                }
            }
            catch
            {//Roll back if discount application fails
                await CompensateAsync(result, cancellationToken);
                throw;
            }

            return result;
        }

        public Task CompensateAsync(AddProductResult result, CancellationToken cancellationToken = default)
        { //Roll Back
            return _productService.CompensateAsync(result, cancellationToken);
        }

        #endregion
    }
}
