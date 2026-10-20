using Ecom.Core.DTO.Products;
using Ecom.Core.Entities.Tbl_Product;

using System.Threading;
using System.Threading.Tasks;

namespace Ecom.Core.Services.Facads
{
    public interface IDiscountFacade
    {
        Task ApplyDiscountAsync(AddDiscountDto discount, CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies a discount to a product that is tracked by the current unit of work but may not
        /// be persisted yet (used by the add-product flow). Links via the product navigation so
        /// the generated foreign key is resolved on save.
        /// </summary>
        Task ApplyDiscountToTrackedProductAsync(AddDiscountDto discount, TblProducts product, CancellationToken cancellationToken = default);

        Task ApplyCategoryDiscountAsync(AddDiscountDto discount, CancellationToken cancellationToken = default);
    }
}