using Ecom.Core.DTO.CartDto;
using Ecom.Core.DTO.CartDto.CartModels;
using Ecom.Core.Exceptions;
using Ecom.Core.Interfaces;
using Ecom.Core.Services;

using ECom.Infrastructure.Application.Facades;

using Moq;

namespace ECom.Api.Tests.Services;

public class CartProductFacadeTests
{
    private const string UserId = "user-1";
    private const int ProductId = 42;

    private readonly Mock<ICartRepository> _cartRepository;
    private readonly Mock<IProductService> _productService;
    private readonly Mock<IStockReservationService> _reservationService;
    private readonly Mock<IReservationJobScheduler> _jobScheduler;
    private readonly CartProductFacade _facade;

    public CartProductFacadeTests()
    {
        _cartRepository = new Mock<ICartRepository>();
        _productService = new Mock<IProductService>();
        _reservationService = new Mock<IStockReservationService>();
        _jobScheduler = new Mock<IReservationJobScheduler>();

        _productService
            .Setup(s => s.GetProductForAddToCartAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(new ProductCartInfoDto
            {
                ProductId = ProductId,
                ProductName = "Widget",
                ProductDescription = "desc",
                BasePrice = 10m,
                FinalPrice = 9m,
                PriceExpiresAt = null,
                AvailableQuantity = 50
            });

        _facade = new CartProductFacade(
            _cartRepository.Object,
            _productService.Object,
            _reservationService.Object,
            _jobScheduler.Object);
    }

    private static Cart CartWith(int quantity) => new()
    {
        UserId = UserId,
        Items = new List<CartItem>
        {
            new()
            {
                ProductId = ProductId,
                ProductName = "Widget",
                Quantity = quantity,
                UnitPrice = 10m
            }
        }
    };

    private static ReserveAdjustmentResult Result(int id, bool newlyCreated, int quantityAfter, DateTime expiresAt) => new()
    {
        ReservationId = id,
        NewlyCreated = newlyCreated,
        ReservedQuantityAfter = quantityAfter,
        ExpiresAtUtc = expiresAt,
        Changed = true
    };

    [Fact]
    public async Task AddProductToCartAsync_NoExistingLine_ReservesFullQuantityAndSchedulesJob()
    {
        _cartRepository.Setup(c => c.GetCartAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cart?)null);
        _reservationService.Setup(r => r.GetReservedQuantityAsync(UserId, ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _reservationService.Setup(r => r.AdjustReservationAsync(UserId, ProductId, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(7, true, 3, DateTime.UtcNow.AddDays(3)));
        _cartRepository.Setup(c => c.SetCartAsync(UserId, It.IsAny<Cart>()))
            .ReturnsAsync(true);

        var cart = await _facade.AddProductToCartAsync(UserId, ProductId, 3, CancellationToken.None);

        Assert.Single(cart.Items);
        Assert.Equal(3, cart.Items[0].Quantity);
        _reservationService.Verify(r => r.AdjustReservationAsync(UserId, ProductId, 3, It.IsAny<CancellationToken>()), Times.Once);
        _jobScheduler.Verify(j => j.ScheduleExpiration(7, It.IsAny<DateTime>()), Times.Once);
        _cartRepository.Verify(c => c.SetCartAsync(UserId, cart), Times.Once);
    }

    [Fact]
    public async Task AddProductToCartAsync_ExistingReservationReservesOnlyTheDelta()
    {
        _cartRepository.Setup(c => c.GetCartAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartWith(2));
        _reservationService.Setup(r => r.GetReservedQuantityAsync(UserId, ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _reservationService.Setup(r => r.AdjustReservationAsync(UserId, ProductId, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(7, false, 5, DateTime.UtcNow.AddDays(3)));
        _cartRepository.Setup(c => c.SetCartAsync(UserId, It.IsAny<Cart>()))
            .ReturnsAsync(true);

        var cart = await _facade.AddProductToCartAsync(UserId, ProductId, 3, CancellationToken.None);

        Assert.Equal(5, cart.Items[0].Quantity);
        _reservationService.Verify(r => r.AdjustReservationAsync(UserId, ProductId, 3, It.IsAny<CancellationToken>()), Times.Once);
        // Not a fresh reservation -> no new expiration job.
        _jobScheduler.Verify(j => j.ScheduleExpiration(It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task AddProductToCartAsync_NotEnoughStock_PropagatesAndNeverPersistsCart()
    {
        _cartRepository.Setup(c => c.GetCartAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cart?)null);
        _reservationService.Setup(r => r.GetReservedQuantityAsync(UserId, ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _reservationService.Setup(r => r.AdjustReservationAsync(UserId, ProductId, 3, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Not enough stock"));

        await Assert.ThrowsAsync<BadRequestException>(() => _facade.AddProductToCartAsync(UserId, ProductId, 3, CancellationToken.None));

        _cartRepository.Verify(c => c.SetCartAsync(It.IsAny<string>(), It.IsAny<Cart>()), Times.Never);
        _jobScheduler.Verify(j => j.ScheduleExpiration(It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCartItemAsync_ReducesQuantity_ReleasesOnlyTheReduction()
    {
        _cartRepository.Setup(c => c.GetCartAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartWith(5));
        _reservationService.Setup(r => r.GetReservedQuantityAsync(UserId, ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        _cartRepository.Setup(c => c.SetCartAsync(UserId, It.IsAny<Cart>()))
            .ReturnsAsync(true);

        var cart = await _facade.UpdateCartItemAsync(UserId, ProductId, 2, CancellationToken.None);

        Assert.Equal(2, cart.Items[0].Quantity);
        _reservationService.Verify(r => r.AdjustReservationAsync(UserId, ProductId, -3, It.IsAny<CancellationToken>()), Times.Once);
        _jobScheduler.Verify(j => j.ScheduleExpiration(It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCartItemAsync_RemovingLine_ReleasesReservedQuantityExactlyOnce()
    {
        _cartRepository.Setup(c => c.GetCartAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartWith(5));
        _reservationService.Setup(r => r.GetReservedQuantityAsync(UserId, ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        _cartRepository.Setup(c => c.SetCartAsync(UserId, It.IsAny<Cart>()))
            .ReturnsAsync(true);

        var cart = await _facade.UpdateCartItemAsync(UserId, ProductId, 0, CancellationToken.None);

        Assert.Empty(cart.Items);
        _reservationService.Verify(r => r.AdjustReservationAsync(UserId, ProductId, -5, It.IsAny<CancellationToken>()), Times.Once);
        _cartRepository.Verify(c => c.SetCartAsync(UserId, cart), Times.Once);
    }

    [Fact]
    public async Task UpdateCartAsync_OmittedLineIsReleasedAndKeptLineIsAdjustedByDelta()
    {
        // Two lines in cart: 42 x 3 (reserved 3), 99 x 2 (reserved 2).
        var cart = new Cart
        {
            UserId = UserId,
            Items = new List<CartItem>
            {
                new() { ProductId = 42, ProductName = "A", Quantity = 3, UnitPrice = 10m },
                new() { ProductId = 99, ProductName = "B", Quantity = 2, UnitPrice = 5m }
            }
        };
        _cartRepository.Setup(c => c.GetCartAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        _reservationService.Setup(r => r.GetReservedQuantityAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, int productId, CancellationToken _) =>
            {
                return productId == 42 ? 3 : 2;
            });
        _reservationService.Setup(r => r.AdjustReservationAsync(UserId, 42, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(7, false, 5, DateTime.UtcNow.AddDays(3)));
        _cartRepository.Setup(c => c.SetCartAsync(UserId, It.IsAny<Cart>()))
            .ReturnsAsync(true);

        var request = new UpdateCartRequest
        {
            userId = UserId,
            Items = new List<CartItemRequest>
            {
                new() { ProductId = 42, Quantity = 5 } // keep 42 -> reserve +2 (5 - 3); 99 omitted -> release 2
            }
        };

        await _facade.UpdateCartAsync(request, CancellationToken.None);

        _reservationService.Verify(r => r.AdjustReservationAsync(UserId, 42, 2, It.IsAny<CancellationToken>()), Times.Once);
        _reservationService.Verify(r => r.AdjustReservationAsync(UserId, 99, -2, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(cart.Items);
        Assert.Equal(5, cart.Items[0].Quantity);
    }

    [Fact]
    public async Task DeleteCartAsync_ReleasesEveryLineExactlyOnceBeforeDeletingCart()
    {
        var cart = new Cart
        {
            UserId = UserId,
            Items = new List<CartItem>
            {
                new() { ProductId = 42, Quantity = 3, UnitPrice = 10m },
                new() { ProductId = 99, Quantity = 2, UnitPrice = 5m }
            }
        };
        _cartRepository.Setup(c => c.GetCartAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        _cartRepository.Setup(c => c.DeleteCartAsync(UserId))
            .ReturnsAsync(true);
        _reservationService.Setup(r => r.GetReservedQuantityAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, int productId, CancellationToken _) =>
            {
                return productId == 42 ? 3 : 2;
            });

        await _facade.DeleteCartAsync(UserId);

        _reservationService.Verify(r => r.AdjustReservationAsync(UserId, 42, -3, It.IsAny<CancellationToken>()), Times.Once);
        _reservationService.Verify(r => r.AdjustReservationAsync(UserId, 99, -2, It.IsAny<CancellationToken>()), Times.Once);
        _reservationService.Verify(r => r.AdjustReservationAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _cartRepository.Verify(c => c.DeleteCartAsync(UserId), Times.Once);
    }

    // ---------- Guest -> user cart merge on login ----------

    [Fact]
    public async Task UpdateCartIdForGuestUsers_NoUserCart_MovesGuestLineAndReservation()
    {
        const string guestId = "guest-1";
        _cartRepository.Setup(c => c.GetCartAsync(guestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartWith(2));
        _cartRepository.Setup(c => c.GetCartAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cart?)null);
        _reservationService.Setup(r => r.GetReservedQuantityAsync(guestId, ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _reservationService.Setup(r => r.GetReservedQuantityAsync(UserId, ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _reservationService.Setup(r => r.AdjustReservationAsync(guestId, ProductId, -2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(1, false, 0, DateTime.UtcNow));
        _reservationService.Setup(r => r.AdjustReservationAsync(UserId, ProductId, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(9, true, 2, DateTime.UtcNow.AddDays(3)));
        _cartRepository.Setup(c => c.SetCartAsync(UserId, It.IsAny<Cart>())).ReturnsAsync(true);

        await _facade.UpdateCartIdForGuestUsers(guestId, UserId, CancellationToken.None);

        // Guest stock is released first, then the merged quantity is reserved under the user.
        _reservationService.Verify(r => r.AdjustReservationAsync(guestId, ProductId, -2, It.IsAny<CancellationToken>()), Times.Once);
        _reservationService.Verify(r => r.AdjustReservationAsync(UserId, ProductId, 2, It.IsAny<CancellationToken>()), Times.Once);
        _cartRepository.Verify(c => c.SetCartAsync(UserId, It.Is<Cart>(cart =>
            cart.UserId == UserId && cart.Items.Count == 1 && cart.Items[0].Quantity == 2)), Times.Once);
        _cartRepository.Verify(c => c.DeleteCartAsync(guestId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCartIdForGuestUsers_MergesDuplicateProductWithoutDoubleReserving()
    {
        const string guestId = "guest-1";
        _cartRepository.Setup(c => c.GetCartAsync(guestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartWith(2));
        _cartRepository.Setup(c => c.GetCartAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartWith(3));
        _reservationService.Setup(r => r.GetReservedQuantityAsync(guestId, ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _reservationService.Setup(r => r.GetReservedQuantityAsync(UserId, ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        _reservationService.Setup(r => r.AdjustReservationAsync(guestId, ProductId, -2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(1, false, 0, DateTime.UtcNow));
        _reservationService.Setup(r => r.AdjustReservationAsync(UserId, ProductId, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(9, false, 5, DateTime.UtcNow.AddDays(3)));
        _cartRepository.Setup(c => c.SetCartAsync(UserId, It.IsAny<Cart>())).ReturnsAsync(true);

        await _facade.UpdateCartIdForGuestUsers(guestId, UserId, CancellationToken.None);

        // 3 (user) + 2 (guest) = 5, and the user already reserves 3 -> only +2 is reserved.
        _reservationService.Verify(r => r.AdjustReservationAsync(UserId, ProductId, 2, It.IsAny<CancellationToken>()), Times.Once);
        _cartRepository.Verify(c => c.SetCartAsync(UserId, It.Is<Cart>(cart =>
            cart.Items.Count == 1 && cart.Items[0].Quantity == 5)), Times.Once);
        _cartRepository.Verify(c => c.DeleteCartAsync(guestId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCartIdForGuestUsers_SameId_IsNoOp()
    {
        await _facade.UpdateCartIdForGuestUsers(UserId, UserId, CancellationToken.None);

        _cartRepository.Verify(c => c.SetCartAsync(It.IsAny<string>(), It.IsAny<Cart>()), Times.Never);
        _reservationService.Verify(r => r.AdjustReservationAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCartIdForGuestUsers_MissingGuestCart_IsNoOp()
    {
        const string guestId = "guest-missing";
        _cartRepository.Setup(c => c.GetCartAsync(guestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cart?)null);

        await _facade.UpdateCartIdForGuestUsers(guestId, UserId, CancellationToken.None);

        _cartRepository.Verify(c => c.SetCartAsync(It.IsAny<string>(), It.IsAny<Cart>()), Times.Never);
        _cartRepository.Verify(c => c.DeleteCartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
