using Ecom.Core.Entities.Order;
using Ecom.Core.Entities.Stock;
using Ecom.Core.Exceptions;
using Ecom.Core.Services;

using ECom.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ECom.Infrastructure.Application.Services
{
    /// <summary>
    /// Owns every stock reservation. Mutations run as atomic, conditional T-SQL inside
    /// an explicit transaction on the scoped AppDbContext (the same context instance the
    /// request's repositories use), so "decrement stock" and "upsert reservation" either
    /// both happen or neither does. Availability is enforced by "Quantity >= delta" in the
    /// UPDATE that consumes stock - not by a read-then-write in application code.
    /// </summary>
    public class StockReservationService : IStockReservationService
    {
        private static readonly TimeSpan ReservationLifetime = TimeSpan.FromDays(3);

        private const string Pending = "Pending";
        private const string AttachedToOrder = "AttachedToOrder";

        private readonly AppDbContext _context;

        public StockReservationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ReserveAdjustmentResult> AdjustReservationAsync(string userId, int productId, int deltaQuantity, CancellationToken cancellationToken = default)
        {
            // مفيش تغيير
            if (deltaQuantity == 0)
            {
                return new ReserveAdjustmentResult { Changed = false };
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            //فيه زياده ف الحجز 
            if (deltaQuantity > 0)
            {
                return await ReserveAsync(userId, productId, deltaQuantity, transaction, cancellationToken);
            }
            //فيه نقص ف الحجز
            return await ReleaseAsync(userId, productId, -deltaQuantity, transaction, cancellationToken);
        }

        public async Task<IReadOnlyList<TblStockReservation>> GetActiveReservationsAsync(string userId, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            return await _context.TblStockReservations
                .Where(r => r.UserId == userId
                            && (r.Status == ReservationStatus.Pending
                                || r.Status == ReservationStatus.AttachedToOrder)
                                && r.ExpiresAt > now)
                .OrderBy(r => r.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<int> GetReservedQuantityAsync(string userId, int productId, CancellationToken cancellationToken = default)
        {
            return await _context.TblStockReservations
                .Where(r => r.UserId == userId
                            && r.ProductId == productId
                            && r.Status == ReservationStatus.Pending)
                .Select(r => r.Quantity)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<int> ExpireDueReservationsAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            // الاوردر معدش موجود او تم الدفع او فشل او عدي معاد صلاحيتها
            var candidates = await _context.TblStockReservations
                .Where(r => (r.Status == ReservationStatus.Pending
                             || r.Status == ReservationStatus.AttachedToOrder)
                            && r.ExpiresAt <= now
                            && (r.OrderId == null
                                || !_context.tblOrders.Any(o => o.Id == r.OrderId
                                    && (o.status == Status.PaymentReceived
                                        || o.status == Status.PaymentFaild))))
                .OrderBy(r => r.Id)
                .Take(200)
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);

            int released = 0;
            foreach (var reservationId in candidates)
            {
                if (await ExpireReservationAsync(reservationId, cancellationToken))
                {
                    released++;
                }
            }
            return released;
        }

        public async Task<bool> ExpireReservationAsync(int reservationId, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            int changed = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE TblStockReservations SET [Status] = N'Expired', [UpdatedAt] = GETUTCDATE() WHERE [Id] = {reservationId} AND [Status] IN ({Pending}, {AttachedToOrder}) AND [ExpiresAt] <= {now} AND ([OrderId] IS NULL OR NOT EXISTS (SELECT 1 FROM tblOrders o WHERE o.Id = TblStockReservations.OrderId AND o.[status] IN (N'PaymentReceived', N'PaymentFaild')))""",
                cancellationToken);

            if (changed == 1)
            {
                var row = await _context.TblStockReservations
                    .Where(r => r.Id == reservationId && r.Status == ReservationStatus.Expired)
                    .Select(r => new { r.ProductId, r.Quantity, r.OrderId })
                    .FirstOrDefaultAsync(cancellationToken);

                if (row != null)
                {
                    // Release exactly once: the Status->Expired UPDATE above is the gate,
                    // a concurrent/repeated call affects 0 rows and restores nothing.
                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE TblProducts SET [Quantity] = [Quantity] + {row.Quantity}, [UpdatedAt] = GETUTCDATE() WHERE [Id] = {row.ProductId}",
                        cancellationToken);

                    if (row.OrderId.HasValue)
                    {
                        // The attached order never got paid in time -> fail it (also idempotent).
                        await _context.Database.ExecuteSqlInterpolatedAsync(
                            $"UPDATE tblOrders SET [status] = N'PaymentFaild' WHERE [Id] = {row.OrderId.Value} AND [status] = N'Pending'",
                            cancellationToken);
                    }
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return changed == 1;
        }

        public async Task<bool> TryConfirmReservationAsync(int reservationId, CancellationToken cancellationToken = default)
        {
            // FUTURE payment confirmation gate. Stock is NOT restored here: a confirmed
            // purchase permanently owns the reserved quantity.
            int changed = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE TblStockReservations SET [Status] = N'Confirmed', [UpdatedAt] = GETUTCDATE() WHERE [Id] = {reservationId} AND [Status] = {AttachedToOrder} AND [OrderId] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM tblOrders o WHERE o.Id = TblStockReservations.OrderId AND o.[status] IN (N'PaymentReceived', N'PaymentFaild'))""",
                cancellationToken);

            return changed == 1;
        }

        public async Task<bool> GetReservationThenConfirm(int orderId,string userId)
        {
            var x = await _context.TblStockReservations.Where(x => x.UserId == userId && x.OrderId == orderId)
                .Select(x=>x.Id).FirstOrDefaultAsync() ;
           return await TryConfirmReservationAsync(x);

        }

        #region Private Helpers

        private async Task<ReserveAdjustmentResult> ReserveAsync(string userId, int productId, int deltaQuantity, IDbContextTransaction transaction, CancellationToken cancellationToken)
        {
            var expiresAt = DateTime.UtcNow.Add(ReservationLifetime);

            // 1) Fast path: an active Pending reservation already exists - just extend it.
            // The EXISTS guard bakes the stock check into the renew so a request for more
            // than what is still available never grows the reservation.
            int renewed = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE TblStockReservations SET [Quantity] = [Quantity] + {deltaQuantity}, [ExpiresAt] = {expiresAt}, [UpdatedAt] = GETUTCDATE() WHERE [UserId] = {userId} AND [ProductId] = {productId} AND [Status] = {Pending} AND EXISTS (SELECT 1 FROM TblProducts p WHERE p.Id = {productId} AND p.[IsDeleted] = 0 AND p.[Quantity] >= {deltaQuantity})""",
                cancellationToken);
            //مفيش تعديل؟
            if (renewed == 0)
            {
                // No Pending reservation. A reservation attached to a placed order must not
                // be doubled by a second, Pending reservation for the same product.
                bool attachedExists = await _context.TblStockReservations
                    .AnyAsync(r => r.UserId == userId
                                   && r.ProductId == productId
                                   && r.Status == ReservationStatus.AttachedToOrder,
                        cancellationToken);

                if (attachedExists)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw new BadRequestException("This product is already reserved for a placed order and cannot be reserved again.");
                }
            }

            // 2) Atomically consume available stock on renew AND on first reserve. The
            // "Quantity >= delta" predicate is the real concurrency guard: two requests for
            // the last unit serialize on this row, so only one can consume.
            int consumed = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE TblProducts SET [Quantity] = [Quantity] - {deltaQuantity}, [UpdatedAt] = GETUTCDATE() WHERE [Id] = {productId} AND [IsDeleted] = 0 AND [Quantity] >= {deltaQuantity}",
                cancellationToken);

            if (consumed == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                // Re-read only to build an honest message while the row is still locked.
                var name = await _context.TblProducts
                    .Where(p => p.Id == productId)
                    .Select(p => p.Name)
                    .FirstOrDefaultAsync(cancellationToken);
                throw new BadRequestException(
                    $"Not enough stock to reserve {deltaQuantity} unit(s) for '{(string.IsNullOrEmpty(name) ? productId.ToString() : name)}'.");
            }

            if (renewed == 1)
            {
                var current = await _context.TblStockReservations
                    .Where(r => r.UserId == userId && r.ProductId == productId && r.Status == ReservationStatus.Pending)
                    .Select(r => new { r.Id, r.Quantity })
                    .FirstAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return new ReserveAdjustmentResult
                {
                    ReservationId = current.Id,
                    NewlyCreated = false,
                    ReservedQuantityAfter = current.Quantity,
                    ExpiresAtUtc = expiresAt,
                    Changed = true
                };
            }

            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""INSERT INTO TblStockReservations ([UserId], [ProductId], [Quantity], [ExpiresAt], [Status], [CreatedAt], [CreatedBy], [IsDeleted]) VALUES ({userId}, {productId}, {deltaQuantity}, {expiresAt}, {Pending}, GETUTCDATE(), {userId}, 0)""",
                cancellationToken);

            var id = await _context.TblStockReservations
                .Where(r => r.UserId == userId && r.ProductId == productId && r.Status == ReservationStatus.Pending)
                .Select(r => r.Id)
                .FirstAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new ReserveAdjustmentResult
            {
                ReservationId = id,
                NewlyCreated = true,
                ReservedQuantityAfter = deltaQuantity,
                ExpiresAtUtc = expiresAt,
                Changed = true
            };
        }

        private async Task<ReserveAdjustmentResult> ReleaseAsync(string userId, int productId, int releaseQuantity, IDbContextTransaction transaction, CancellationToken cancellationToken)
        {
            // Only cart-owned (Pending) reservations can be released here. AttachedToOrder
            // reservations belong to a placed order and are handled by expiry/payment.
            int changed = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE TblStockReservations SET [Quantity] = [Quantity] - {releaseQuantity}, [Status] = CASE WHEN [Quantity] - {releaseQuantity} <= 0 THEN N'Released' ELSE [Status] END, [UpdatedAt] = GETUTCDATE() WHERE [UserId] = {userId} AND [ProductId] = {productId} AND [Status] = {Pending} AND [Quantity] >= {releaseQuantity}""",
                cancellationToken);

            if (changed == 0)
            {
                // Nothing to release: the reservation is already gone or smaller. No-op.
                await transaction.CommitAsync(cancellationToken);
                return new ReserveAdjustmentResult { Changed = false };
            }

            int remaining = await _context.TblStockReservations
                .Where(r => r.UserId == userId && r.ProductId == productId && r.Status == ReservationStatus.Pending)
                .Select(r => r.Quantity)
                .FirstOrDefaultAsync(cancellationToken);

            // Return exactly what the Pending reservation gave up.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE TblProducts SET [Quantity] = [Quantity] + {releaseQuantity}, [UpdatedAt] = GETUTCDATE() WHERE [Id] = {productId}",
                cancellationToken);

            var reservationId = await _context.TblStockReservations
                .Where(r => r.UserId == userId && r.ProductId == productId && r.Status == ReservationStatus.Pending)
                .Select(r => r.Id)
                .FirstOrDefaultAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new ReserveAdjustmentResult
            {
                ReservationId = reservationId,
                NewlyCreated = false,
                ReservedQuantityAfter = remaining,
                Changed = true
            };
        }

        #endregion
    }
}
