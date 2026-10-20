using Ecom.Core.Entities.Discount;
using Ecom.Core.Entities.Tbl_Product;

namespace Ecom.Core.Interfaces
{
    public interface IDiscountProductRepo : IGenericRepo<TblProductDiscount, int>
    {
        Task<TblProductDiscount> AddDiscountProductAsync(TblDiscount discount, int productId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Links a discount to an already-tracked (but possibly not yet saved) product using the
        /// navigation property. This lets EF Core insert the product before the join row and
        /// resolve the generated foreign key, which the scalar <c>ProductId</c> overload cannot
        /// do while the product id is still 0.
        /// </summary>
        Task<TblProductDiscount> AddDiscountProductAsync(TblDiscount discount, TblProducts product, CancellationToken cancellationToken = default);

        Task AddDiscountToProductsAsync(TblDiscount discount, IEnumerable<int> productIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads the discount links for the given products, including each linked <c>Discount</c>.
        /// Used by the read path to resolve the effective price; soft-deleted links and soft-deleted
        /// discounts are excluded. Deliberately does NOT filter on the active date window so that
        /// IDiscountPriceCalculator stays the only definition of "active".
        /// </summary>
        Task<IReadOnlyList<TblProductDiscount>> GetByProductIdsAsync(IEnumerable<int> productIds, CancellationToken cancellationToken = default);
    }
}
