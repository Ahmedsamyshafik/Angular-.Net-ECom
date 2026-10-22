using Ecom.Core.Entities.Order;
using Ecom.Core.Entities.Stock;
using Ecom.Core.Entities.Tbl_Product;
using Ecom.Core.Exceptions;
using Ecom.Core.Services;
using ECom.Infrastructure.Application.Services;
using ECom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECom.Api.Tests.Services;

/// <summary>
/// Integration tests that run against a REAL SQL Server instance (the only honest way to
/// exercise the atomic conditional T-SQL, filtered unique index and RowVersion gates that
/// plain InMemory/mock unit tests cannot emulate). They use a dedicated test database
/// (AngularDotNetEcom_ReservationTests), never the production one, and are skipped silently
/// when SQL Server is not reachable or ECOM_SKIP_SQL_TESTS=1 is set.
/// </summary>
public class StockReservationServiceSqlTests
{
    private const string ConnectionString =
        "Server=localhost;Database=AngularDotNetEcom_ReservationTests;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5";

    private static readonly bool _skipByEnv =
        string.Equals(Environment.GetEnvironmentVariable("ECOM_SKIP_SQL_TESTS"), "1", StringComparison.OrdinalIgnoreCase);

    private static bool? _sqlAvailable;

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<bool> SqlAvailableAsync()
    {
        if (_skipByEnv)
        {
            return false;
        }
        if (_sqlAvailable.HasValue)
        {
            return _sqlAvailable.Value;
        }
        try
        {
            await using var probe = CreateContext();
            await probe.Database.EnsureCreatedAsync();
            _sqlAvailable = true;
        }
        catch
        {
            _sqlAvailable = false;
        }
        return _sqlAvailable.Value;
    }

    private static string NewUser() => "u-" + Guid.NewGuid().ToString("N");

    private static async Task<int> CreateProductAsync(AppDbContext ctx, int quantity)
    {
        var category = new TblCategories { Name = "cat-" + Guid.NewGuid().ToString("N")[..8], Description = "d" };
        ctx.TblCategories.Add(category);
        await ctx.SaveChangesAsync();

        var product = new TblProducts
        {
            Name = "p-" + Guid.NewGuid().ToString("N")[..8],
            Description = "d",
            Price = 10m,
            Quantity = quantity,
            CategoryId = category.Id
        };
        ctx.TblProducts.Add(product);
        await ctx.SaveChangesAsync();
        return product.Id;
    }

    private static async Task<int> CreateReservationAsync(string userId, int productId, int quantity)
    {
        await using var ctx = CreateContext();
        var result = await new StockReservationService(ctx)
            .AdjustReservationAsync(userId, productId, quantity, CancellationToken.None);
        return result.ReservationId;
    }

    private static async Task<int> CreateOrderAsync()
    {
        await using var ctx = CreateContext();
        var deliveryMethod = new TblDeliveryMethod { Name = "test", Description = "d", Price = 10m, DeliveryTime = "1d" };
        ctx.tblDeliveryMethods.Add(deliveryMethod);

        var order = new TblOrders(
            "buyer-" + Guid.NewGuid().ToString("N")[..8] + "@test.com",
            10m,
            new TblShippingAddress { FirstName = "a", LastName = "b", City = "c", ZipCode = "z", Street = "s", State = "st" },
            deliveryMethod,
            Array.Empty<TblOrderItem>());
        ctx.tblOrders.Add(order);
        await ctx.SaveChangesAsync();
        return order.Id;
    }

    /// <summary>
    /// Attaches a Pending reservation to an existing order, sets its ExpiresAt into the past
    /// (as the 3-day window would eventually do), and sets the order status for the scenario.
    /// Returns the reservation id.
    /// </summary>
    private static async Task<int> AttachReservationToOrderAsync(
        string userId,
        int productId,
        int orderId,
        int? reservationId = null,
        Status orderStatus = Status.Pending)
    {
        await using var ctx = CreateContext();
        var order = await ctx.tblOrders.FirstAsync(o => o.Id == orderId);
        order.status = orderStatus;

        var reservation = await ctx.TblStockReservations
            .Where(r => (reservationId == null ? r.UserId == userId && r.ProductId == productId : r.Id == reservationId)
                        && r.Status == ReservationStatus.Pending)
            .FirstAsync();

        reservation.Status = ReservationStatus.AttachedToOrder;
        reservation.OrderId = order.Id;
        reservation.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await ctx.SaveChangesAsync();
        return reservation.Id;
    }

    private static async Task<int> AvailableAsync(int productId)
    {
        await using var ctx = CreateContext();
        return await ctx.TblProducts.Where(p => p.Id == productId).Select(p => p.Quantity).FirstAsync();
    }

    [Fact]
    public async Task Reserve_DecrementsAvailableStock_ReleaseRestoresExactlyOnce()
    {
        if (!await SqlAvailableAsync())
        {
            return;
        }

        string user = NewUser();
        await using var ctx = CreateContext();
        var service = new StockReservationService(ctx);
        int productId = await CreateProductAsync(ctx, 10);

        var created = await service.AdjustReservationAsync(user, productId, 4, CancellationToken.None);

        Assert.True(created.NewlyCreated);
        Assert.Equal(4, created.ReservedQuantityAfter);
        Assert.Equal(6, await AvailableAsync(productId));
        Assert.Equal(4, await service.GetReservedQuantityAsync(user, productId, CancellationToken.None));

        var released = await service.AdjustReservationAsync(user, productId, -4, CancellationToken.None);

        Assert.True(released.Changed);
        Assert.Equal(10, await AvailableAsync(productId));
        Assert.Equal(0, await service.GetReservedQuantityAsync(user, productId, CancellationToken.None));
    }

    [Fact]
    public async Task Reserve_MoreThanAvailable_ThrowsBadRequestAndPersistsNothing()
    {
        if (!await SqlAvailableAsync())
        {
            return;
        }

        string user = NewUser();
        await using var ctx = CreateContext();
        var service = new StockReservationService(ctx);
        int productId = await CreateProductAsync(ctx, 2);

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.AdjustReservationAsync(user, productId, 3, CancellationToken.None));

        Assert.Equal(2, await AvailableAsync(productId));
        Assert.Equal(0, await service.GetReservedQuantityAsync(user, productId, CancellationToken.None));
    }

    [Fact]
    public async Task ConcurrentRequestsForLastUnit_ExactlyOneReservationSucceeds()
    {
        if (!await SqlAvailableAsync())
        {
            return;
        }

        string user = NewUser();
        await using var ctxSeed = CreateContext();
        int productId = await CreateProductAsync(ctxSeed, 1);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => TryReserveOneAsync(user, productId)));

        Assert.Equal(1, outcomes.Count(o => o));
        Assert.Equal(0, await AvailableAsync(productId));
        Assert.Equal(1, await GetReservedAsync(user, productId));
    }

    private static async Task<bool> TryReserveOneAsync(string user, int productId)
    {
        await using var ctx = CreateContext();
        try
        {
            await new StockReservationService(ctx)
                .AdjustReservationAsync(user, productId, 1, CancellationToken.None);
            return true;
        }
        catch (BadRequestException)
        {
            return false;
        }
    }

    private static async Task<int> GetReservedAsync(string user, int productId)
    {
        await using var ctx = CreateContext();
        return await new StockReservationService(ctx).GetReservedQuantityAsync(user, productId, CancellationToken.None);
    }

    [Fact]
    public async Task Update_ReservesOnlyTheDelta()
    {
        if (!await SqlAvailableAsync())
        {
            return;
        }

        string user = NewUser();
        await using var ctx = CreateContext();
        var service = new StockReservationService(ctx);
        int productId = await CreateProductAsync(ctx, 5);

        await service.AdjustReservationAsync(user, productId, 2, CancellationToken.None);
        await service.AdjustReservationAsync(user, productId, 1, CancellationToken.None);

        Assert.Equal(3, await service.GetReservedQuantityAsync(user, productId, CancellationToken.None));
        Assert.Equal(2, await AvailableAsync(productId));
    }

    [Fact]
    public async Task RemoveThenRepeatRelease_StockIsRestoredOnlyOnce()
    {
        if (!await SqlAvailableAsync())
        {
            return;
        }

        string user = NewUser();
        await using var ctx = CreateContext();
        var service = new StockReservationService(ctx);
        int productId = await CreateProductAsync(ctx, 5);

        await service.AdjustReservationAsync(user, productId, 3, CancellationToken.None);
        var first = await service.AdjustReservationAsync(user, productId, -3, CancellationToken.None);
        var second = await service.AdjustReservationAsync(user, productId, -3, CancellationToken.None);

        Assert.True(first.Changed);
        Assert.False(second.Changed);
        Assert.Equal(5, await AvailableAsync(productId));
        Assert.Equal(0, await service.GetReservedQuantityAsync(user, productId, CancellationToken.None));
    }

    [Fact]
    public async Task Expire_ReleasesStockOnce_AndIsIdempotent()
    {
        if (!await SqlAvailableAsync())
        {
            return;
        }

        string user = NewUser();
        await using var ctx = CreateContext();
        int productId = await CreateProductAsync(ctx, 5);
        int reservationId = await CreateReservationAsync(user, productId, 3);

        await ForceExpiryAsync(reservationId);

        await using var expireCtx = CreateContext();
        var service = new StockReservationService(expireCtx);

        Assert.True(await service.ExpireReservationAsync(reservationId, CancellationToken.None));
        Assert.Equal(5, await AvailableAsync(productId));

        Assert.False(await service.ExpireReservationAsync(reservationId, CancellationToken.None));
        Assert.Equal(5, await AvailableAsync(productId));
        Assert.Equal(0, await GetReservedAsync(user, productId));
    }

    [Fact]
    public async Task Expire_AttachedToPaidOrder_IsNotReleased()
    {
        if (!await SqlAvailableAsync())
        {
            return;
        }

        string user = NewUser();
        await using var ctx = CreateContext();
        int productId = await CreateProductAsync(ctx, 5);
        int reservationId = await CreateReservationAsync(user, productId, 3);
        int orderId = await CreateOrderAsync();

        await AttachReservationToOrderAsync(user, productId, orderId, reservationId, Status.PaymentReceived);

        await using var expireCtx = CreateContext();
        var service = new StockReservationService(expireCtx);

        Assert.False(await service.ExpireReservationAsync(reservationId, CancellationToken.None));
        Assert.Equal(2, await AvailableAsync(productId));
    }

    [Fact]
    public async Task ConfirmThenExpire_ConfirmWins_NoStockRestored()
    {
        if (!await SqlAvailableAsync())
        {
            return;
        }

        string user = NewUser();
        await using var ctx = CreateContext();
        int productId = await CreateProductAsync(ctx, 5);
        int reservationId = await CreateReservationAsync(user, productId, 3);
        int orderId = await CreateOrderAsync();

        await AttachReservationToOrderAsync(user, productId, orderId, reservationId, Status.Pending);

        await using var confirmCtx = CreateContext();
        var service = new StockReservationService(confirmCtx);

        // Payment confirmation wins the race.
        Assert.True(await service.TryConfirmReservationAsync(reservationId, CancellationToken.None));
        // Expiry afterwards is a no-op.
        Assert.False(await service.ExpireReservationAsync(reservationId, CancellationToken.None));
        Assert.Equal(2, await AvailableAsync(productId));
    }

    [Fact]
    public async Task ExpireThenConfirm_ExpireWins_OrderFailedAndConfirmRefused()
    {
        if (!await SqlAvailableAsync())
        {
            return;
        }

        string user = NewUser();
        await using var ctx = CreateContext();
        int productId = await CreateProductAsync(ctx, 5);
        int reservationId = await CreateReservationAsync(user, productId, 3);
        int orderId = await CreateOrderAsync();

        await AttachReservationToOrderAsync(user, productId, orderId, reservationId, Status.Pending);

        await using var expireCtx = CreateContext();
        var service = new StockReservationService(expireCtx);

        // Expiry wins: stock restored and the still-pending order is failed.
        Assert.True(await service.ExpireReservationAsync(reservationId, CancellationToken.None));
        Assert.Equal(5, await AvailableAsync(productId));

        await using var orderCtx = CreateContext();
        var failedOrder = await orderCtx.tblOrders.FirstAsync(o => o.Id == orderId);
        Assert.Equal(Status.PaymentFaild, failedOrder.status);

        // Payment confirmation is refused: a reservation that expired cannot be confirmed.
        Assert.False(await service.TryConfirmReservationAsync(reservationId, CancellationToken.None));
    }

    [Fact]
    public async Task OrderAttachOnStaleRowVersion_ThrowsDbUpdateConcurrencyException()
    {
        if (!await SqlAvailableAsync())
        {
            return;
        }

        string user = NewUser();
        await using var seedCtx = CreateContext();
        int productId = await CreateProductAsync(seedCtx, 5);
        await CreateReservationAsync(user, productId, 2);

        // Context A loads the reservation (captures its RowVersion).
        await using var ctxA = CreateContext();
        var a = await ctxA.TblStockReservations
            .Where(r => r.UserId == user && r.ProductId == productId)
            .FirstAsync();

        // Context B modifies and saves it -> RowVersion advances (simulates the order-attach
        // racing with the Hangfire expiration job in the real flow).
        await using var ctxB = CreateContext();
        var b = await ctxB.TblStockReservations
            .Where(r => r.Id == a.Id)
            .FirstAsync();
        b.Quantity = 1;
        await ctxB.SaveChangesAsync();

        // Context A tries to save its now-stale copy: must surface a concurrency conflict.
        a.Quantity = 3;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ctxA.SaveChangesAsync());
    }

    [Fact]
    public async Task GetReservationThenConfirm_ConfirmsAllReservationsForTheOrder()
    {
        if (!await SqlAvailableAsync())
        {
            return;
        }

        string user = NewUser();
        await using var ctx = CreateContext();
        int productA = await CreateProductAsync(ctx, 5);
        int productB = await CreateProductAsync(ctx, 5);
        int reservationA = await CreateReservationAsync(user, productA, 2);
        int reservationB = await CreateReservationAsync(user, productB, 3);
        int orderId = await CreateOrderAsync();

        await AttachReservationToOrderAsync(user, productA, orderId, reservationA, Status.Pending);
        await AttachReservationToOrderAsync(user, productB, orderId, reservationB, Status.Pending);

        await using var confirmCtx = CreateContext();
        var service = new StockReservationService(confirmCtx);

        Assert.True(await service.GetReservationThenConfirm(orderId, user));

        await using var verifyCtx = CreateContext();
        var statuses = await verifyCtx.TblStockReservations
            .Where(r => r.OrderId == orderId)
            .Select(r => r.Status)
            .ToListAsync();

        Assert.Equal(2, statuses.Count);
        Assert.All(statuses, s => Assert.Equal(ReservationStatus.Confirmed, s));
    }

    private static async Task ForceExpiryAsync(int reservationId)
    {
        await using var ctx = CreateContext();
        var reservation = await ctx.TblStockReservations.FirstAsync(r => r.Id == reservationId);
        reservation.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await ctx.SaveChangesAsync();
    }
}