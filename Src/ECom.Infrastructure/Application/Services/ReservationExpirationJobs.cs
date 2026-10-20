using Ecom.Core.Services;

namespace ECom.Infrastructure.Application.Services
{
    /// <summary>
    /// Hangfire entry points for reservation expiration. Hangfire's scheduling/recurring
    /// API only accepts expression returning plain Task, so these wrappers expose the
    /// service's Task&lt;bool&gt;/Task&lt;int&gt; methods as Task to keep the core interface
    /// useful for tests and callers.
    /// </summary>
    public class ReservationExpirationJobs
    {
        private readonly IStockReservationService _reservationService;

        public ReservationExpirationJobs(IStockReservationService reservationService)
        {
            _reservationService = reservationService;
        }

        public Task ExpireReservationAsync(int reservationId)
            => _reservationService.ExpireReservationAsync(reservationId);

        public Task ExpireDueReservationsAsync()
            => _reservationService.ExpireDueReservationsAsync();
    }
}