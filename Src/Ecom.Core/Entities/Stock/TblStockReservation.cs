using Ecom.Core.Entities.Order;
using Ecom.Core.Entities.Tbl_Product;

namespace Ecom.Core.Entities.Stock
{
    /// <summary>
    /// One row per active (or historical) stock reservation.
    /// We never create a second row for the same product while an active row exists;
    /// a filtered unique index on (UserId, ProductId) enforces that at the database level.
    /// </summary>
    public class TblStockReservation : BaseEntity<int>
    {
        public string UserId { get; set; }

        public int ProductId { get; set; }

        public int Quantity { get; set; }

        public DateTime ExpiresAt { get; set; }

        public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

        public int? OrderId { get; set; }

        public TblOrders? Order { get; set; }

        public TblProducts? Product { get; set; }
    }
}