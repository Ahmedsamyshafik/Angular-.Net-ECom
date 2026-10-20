namespace Ecom.Core.Entities.Order
{
    /// <summary>
    /// The single address table (tblShippingAddresses) of the system. It serves both
    /// owners: order addresses point at their TblOrders row (OrderId set, AppUserId
    /// null) and the signed-in user's saved address points at their AppUser row
    /// (AppUserId set, OrderId null).
    /// </summary>
    public class TblShippingAddress : BaseEntity<int>
    {
        public TblShippingAddress()
        {
        }

        public TblShippingAddress(string firstName, string lastName, string city, string zipCode, string street, string state)
        {
            FirstName = firstName;
            LastName = lastName;
            City = city;
            ZipCode = zipCode;
            Street = street;
            State = state;
        }

        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string City { get; set; }
        public string ZipCode { get; set; }
        public string Street { get; set; }
        public string State { get; set; }

        // Set when this row is the shipping address of an order.
        public int? OrderId { get; set; }

        // Set when this row is the saved address of a user (Account/UpdateAddress).
        public string? AppUserId { get; set; }
    }
}
