using Ecom.Core.Services;

using Hangfire;

namespace ECom.Infrastructure.Application.Services
{
    /// <summary>
    /// Schedules a one-shot Hangfire job that expires a specific reservation 3 days out.
    /// The job itself is idempotent (StockReservationService.ExpireReservationAsync), so a
    /// late run or a re-run has no effect. A minutely recurring sweep
    /// (see Program.cs) is kept as a safety net for lost/never-scheduled jobs.
    /// </summary>
    public class HangfireReservationJobScheduler : IReservationJobScheduler
    {
        public void ScheduleExpiration(int reservationId, DateTime expiresAtUtc)
        {
            var delay = expiresAtUtc - DateTime.UtcNow;
            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
            }

            BackgroundJob.Schedule<ReservationExpirationJobs>(
                job => job.ExpireReservationAsync(reservationId),
                delay);
        }
    }
}