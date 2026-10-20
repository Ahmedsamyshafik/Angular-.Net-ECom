using Ecom.Core.Entities.Order;

namespace Ecom.Core.Interfaces
{
    public interface IShippingAddressesRepo : IGenericRepo<TblShippingAddress, int>
    {
        /// <summary>
        /// Returns the address the given user saved in the shared address table
        /// (tblShippingAddresses), or null when the user never saved one.
        /// </summary>
        Task<TblShippingAddress?> GetByUserAsync(string userId, CancellationToken cancellationToken = default);
    }
}
