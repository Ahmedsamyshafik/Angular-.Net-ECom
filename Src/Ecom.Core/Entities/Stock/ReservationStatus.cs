namespace Ecom.Core.Entities.Stock
{
    /// <summary>
    /// Lifecycle of a stock reservation.
    /// Pending        - created by the cart, still owned by the basket;
    /// AttachedToOrder- bound to a placed (but still unpaid) order;
    /// Confirmed      - payment completed (future payment confirmation point);
    /// Released       - returned to available stock because the cart line was removed;
    /// Expired        - the 3-day window passed (or the attached order was never paid).
    /// </summary>
    public enum ReservationStatus
    {
        Pending,
        AttachedToOrder,
        Confirmed,
        Released,
        Expired
    }
}