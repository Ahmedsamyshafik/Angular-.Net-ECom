using Ecom.Core.Entities.Stock;

namespace Ecom.Core.Services
{
    /// <summary>
    /// Outcome of a reservation adjustment (reserve or release).
    /// The caller uses NewlyCreated to decide whether a Hangfire expiration job
    /// must be scheduled for this reservation.
    /// </summary>
    public class ReserveAdjustmentResult
    {
        public int ReservationId { get; set; }

        public bool NewlyCreated { get; set; }

        public int ReservedQuantityAfter { get; set; }

        public DateTime ExpiresAtUtc { get; set; }

        public bool Changed { get; set; }
    }

    /// <summary>
    /// The single owner of stock reservations.
    /// Every mutating operation is executed atomically in the database (single
    /// transaction with conditional UPDATEs), so the available quantity on
    /// TblProducts.Quantity is always in sync with the active reservations.
    /// </summary>
    public interface IStockReservationService
    {
        /// <summary>
        /// Adjusts the active Pending reservation for (userId, productId) by deltaQuantity.
        /// Positive delta consumes available stock atomically; negative delta returns it.
        /// Throws a BadRequestException when there is not enough available stock.
        /// </summary>
        Task<ReserveAdjustmentResult> AdjustReservationAsync(
            string userId,
            int productId,
            int deltaQuantity,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns every active (Pending or AttachedToOrder) reservation for the user.
        /// Entities returned are tracked by the current DbContext so the caller can
        /// attach them to a new order and persist both in the same SaveChanges.
        /// </summary>
        Task<IReadOnlyList<TblStockReservation>> GetActiveReservationsAsync(
            string userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Current reserved quantity (Pending reservations only, i.e. cart-owned)
        /// for (userId, productId). 0 when no Pending reservation exists.
        /// </summary>
        Task<int> GetReservedQuantityAsync(
            string userId,
            int productId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Idempotent sweep: releases every reservation whose ExpiresAt has passed and
        /// whose attached order (if any) is still Pending. Returns the number released.
        /// </summary>
        Task<int> ExpireDueReservationsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases exactly one reservation (scheduled Hangfire job / sweep). Idempotent:
        /// a second call has no effect. Never releases an order that was already paid.
        /// Returns true when this call performed the release.
        /// </summary>
        Task<bool> ExpireReservationAsync(int reservationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// FUTURE payment confirmation gate: atomically Pending/AttachedToOrder -> Confirmed.
        /// Returns false when the reservation was already expired/released/confirmed, or the
        /// order is no longer Pending. Intended to be called once the payment provider
        /// confirms a purchase (see the TODO in OrderController.Create).
        /// </summary>
        Task<bool> TryConfirmReservationAsync(int reservationId, CancellationToken cancellationToken = default);

        Task<bool> GetReservationThenConfirm(int orderId, string userId);
    }
}