using Ecom.Core.DTO.CartDto.CartModels;
using Ecom.Core.DTO.CartDto.Order;
using Ecom.Core.Entities.Order;
using Ecom.Core.Entities.Stock;
using Ecom.Core.Entities.User;
using Ecom.Core.Exceptions;
using Ecom.Core.Interfaces;
using Ecom.Core.Services;

using ECom.Api.Tests.Mapping;
using ECom.Infrastructure.Application.Services;

using Moq;

namespace ECom.Api.Tests.Services;

public class OrderServiceReservationTests
{
    private const string Email = "buyer@example.com";
    private const string UserId = "user-1";

    private readonly Mock<IMyAuth> _authService;
    private readonly Mock<IDeliveryMethodsRepo> _deliveryMethodsRepo;
    private readonly Mock<ICartRepository> _cartRepository;
    private readonly Mock<IOrdersRepo> _orderRepository;
    private readonly Mock<IProductService> _productService;
    private readonly Mock<IStockReservationService> _reservationService;
    private readonly OrderService _service;

    public OrderServiceReservationTests()
    {
        _authService = new Mock<IMyAuth>();
        _deliveryMethodsRepo = new Mock<IDeliveryMethodsRepo>();
        _cartRepository = new Mock<ICartRepository>();
        _orderRepository = new Mock<IOrdersRepo>();
        _productService = new Mock<IProductService>();
        _reservationService = new Mock<IStockReservationService>();

        _authService.Setup(a => a.GetUserByEmail(Email))
            .ReturnsAsync(new AppUser { Id = UserId, Email = Email, UserName = "buyer" });

        _deliveryMethodsRepo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>()))
            .ReturnsAsync(new TblDeliveryMethod("Standard", "d", 10m, "3-5 days"));

        _orderRepository.Setup(r => r.AddAsync(It.IsAny<TblOrders>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TblOrders order, CancellationToken _) => order);

        _service = new OrderService(
            MappingTestHelper.CreateMapper(),
            _authService.Object,
            _deliveryMethodsRepo.Object,
            _cartRepository.Object,
            _orderRepository.Object,
            _productService.Object,
            _reservationService.Object);
    }

    private void SetupProduct(int productId, string name)
    {
        _productService.Setup(s => s.GetProductForAddToCartAsync(productId, It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(new Ecom.Core.DTO.CartDto.ProductCartInfoDto
            {
                ProductId = productId,
                ProductName = name,
                BasePrice = 10m,
                FinalPrice = 10m,
                AvailableQuantity = 100
            });
    }

    private static Cart CartWith(params (int ProductId, int Quantity)[] lines) => new()
    {
        UserId = UserId,
        Items = lines.Select(l => new CartItem
        {
            ProductId = l.ProductId,
            ProductName = $"P{l.ProductId}",
            Quantity = l.Quantity,
            UnitPrice = 10m
        }).ToList()
    };

    private static OrderDto OrderDto() => new()
    {
        deliveryMethodId = 1,
        //basketId = UserId,
        shipAddress = new shipAddressDto
        {
            FirstName = "a",
            LastName = "b",
            City = "c",
            ZipCode = "z",
            Street = "s",
            State = "st"
        }
    };

    [Fact]
    public async Task CreateOrderAsync_ValidReservations_AttachesPendingReservationsToOrder()
    {
        SetupProduct(42, "Widget");
        SetupProduct(99, "Gadget");
        _cartRepository.Setup(c => c.GetCartAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartWith((42, 3), (99, 2)));

        var reservations = new List<TblStockReservation>
        {
            new() { Id = 1, UserId = UserId, ProductId = 42, Quantity = 3, Status = ReservationStatus.Pending },
            new() { Id = 2, UserId = UserId, ProductId = 99, Quantity = 5, Status = ReservationStatus.Pending }
        };
        _reservationService.Setup(r => r.GetActiveReservationsAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservations);

        var order = await _service.CreateOrderAsync(Email, OrderDto(), CancellationToken.None);

        Assert.NotNull(order);
        Assert.All(reservations, r => Assert.Equal(ReservationStatus.AttachedToOrder, r.Status));
        Assert.All(reservations, r => Assert.Same(order, r.Order));
        // Quantity is snapped to what the order actually took.
        Assert.Equal(3, reservations[0].Quantity);
        Assert.Equal(2, reservations[1].Quantity);
    }

    [Fact]
    public async Task CreateOrderAsync_ProductReadSkipsRawStockEnforcement()
    {
        SetupProduct(42, "Widget");
        _cartRepository.Setup(c => c.GetCartAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartWith((42, 3)));
        _reservationService.Setup(r => r.GetActiveReservationsAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TblStockReservation>
            {
                new() { Id = 1, UserId = UserId, ProductId = 42, Quantity = 3, Status = ReservationStatus.Pending }
            });

        bool? receivedEnforceFlag = null;
        _productService
            .Setup(s => s.GetProductForAddToCartAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .Callback<int, int, CancellationToken, bool>((_, _, _, enforceAvailableStock) => receivedEnforceFlag = enforceAvailableStock)
            .ReturnsAsync(new Ecom.Core.DTO.CartDto.ProductCartInfoDto
            {
                ProductId = 42,
                ProductName = "Widget",
                BasePrice = 10m,
                FinalPrice = 10m,
                AvailableQuantity = 0 // raw stock exhausted by other reservations
            });

        var order = await _service.CreateOrderAsync(Email, OrderDto(), CancellationToken.None);

        Assert.NotNull(order);
        Assert.False(receivedEnforceFlag, "Order-time reads must not enforce raw available stock.");
    }

    [Fact]
    public async Task CreateOrderAsync_ReservationSmallerThanLine_ThrowsBadRequest()
    {
        SetupProduct(42, "Widget");
        _cartRepository.Setup(c => c.GetCartAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartWith((42, 4)));
        _reservationService.Setup(r => r.GetActiveReservationsAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TblStockReservation>
            {
                new() { Id = 1, UserId = UserId, ProductId = 42, Quantity = 3, Status = ReservationStatus.Pending }
            });

        await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateOrderAsync(Email, OrderDto(), CancellationToken.None));
        _orderRepository.Verify(r => r.AddAsync(It.IsAny<TblOrders>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrderAsync_MissingReservationForLine_ThrowsBadRequest()
    {
        SetupProduct(42, "Widget");
        _cartRepository.Setup(c => c.GetCartAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartWith((42, 1)));
        _reservationService.Setup(r => r.GetActiveReservationsAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TblStockReservation>());

        await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateOrderAsync(Email, OrderDto(), CancellationToken.None));
    }
}