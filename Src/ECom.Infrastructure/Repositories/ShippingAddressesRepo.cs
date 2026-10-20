using Ecom.Core.Entities.Order;
using Ecom.Core.Interfaces;

using ECom.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;

namespace ECom.Infrastructure.Repositories
{
    public class ShippingAddressesRepo : GenericRepo<TblShippingAddress, int>, IShippingAddressesRepo
    {
        public ShippingAddressesRepo(AppDbContext context) : base(context)
        {
        }

        public async Task<TblShippingAddress?> GetByUserAsync(string userId, CancellationToken cancellationToken = default)
        {
            // The shared address table holds one row per user (AppUserId) plus the
            // order rows (OrderId), so the lookup is filtered on the user FK.
            return await _context.tblShippingAddresses
                .FirstOrDefaultAsync(x => x.AppUserId == userId, cancellationToken);
        }
    }
}
