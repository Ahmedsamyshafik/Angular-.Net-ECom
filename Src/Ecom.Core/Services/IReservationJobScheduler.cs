namespace Ecom.Core.Services
{
    /// <summary>
    /// Schedules the delayed Hangfire job that expires a stock reservation.
    /// Implemented with Hangfire in Infrastructure; abstracted here so cart/order
    /// logic stays unit-testable without a Hangfire storage.
    /// </summary>
    public interface IReservationJobScheduler
    {
        void ScheduleExpiration(int reservationId, DateTime expiresAtUtc);
    }
}