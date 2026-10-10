using Ecom.Core.Entities.Discount;

namespace Ecom.Core.Interfaces
{
    public interface IDiscountProductRepo : IGenericRepo<TblProductDiscount, int>
    {
        Task<TblProductDiscount> AddDiscountProductAsync(TblDiscount discount, int productId, CancellationToken cancellationToken = default);

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
