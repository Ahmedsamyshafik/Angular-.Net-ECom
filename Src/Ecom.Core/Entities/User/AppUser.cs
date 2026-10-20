using Ecom.Core.Entities.Order;

using Microsoft.AspNetCore.Identity;

namespace Ecom.Core.Entities.User
{
    public class AppUser : IdentityUser
    {
        public string DisplayName { get; set; }

        // The user's saved address lives in the SAME table used for order shipping
        // addresses (tblShippingAddresses) - there is only one address table in the
        // database, it handles both the user and the order side.
        public TblShippingAddress? ShippingAddress { get; set; }
    }
}
