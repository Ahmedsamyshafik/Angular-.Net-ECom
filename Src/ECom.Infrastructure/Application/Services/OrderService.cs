using AutoMapper;

using Ecom.Core.DTO.CartDto.CartModels;
using Ecom.Core.DTO.CartDto.Order;
using Ecom.Core.Entities.Order;
using Ecom.Core.Entities.Stock;
using Ecom.Core.Exceptions;
using Ecom.Core.Interfaces;
using Ecom.Core.Services;

namespace ECom.Infrastructure.Application.Services
{
    public class OrderService : IOrderService
    {

        private readonly IMapper _mapper;
        private readonly IMyAuth _authService;
        private readonly IDeliveryMethodsRepo _deliveryMethodsRepo;
        private readonly ICartRepository _cartRepository;
        private readonly IOrdersRepo _orderRepository;
        private readonly IProductService _productService;
        private readonly IStockReservationService _reservationService;


        public OrderService(IMapper mapper, IMyAuth authService, IDeliveryMethodsRepo deliveryMethodsRepo, ICartRepository cartRepository, IOrdersRepo orderRepository, IProductService productService, IStockReservationService reservationService)
        {
            _mapper = mapper;
            _authService = authService;
            _deliveryMethodsRepo = deliveryMethodsRepo;
            _cartRepository = cartRepository;
            _orderRepository = orderRepository;
            _productService = productService;
            _reservationService = reservationService;
        }

        public async Task<TblOrders> CreateOrderAsync(string BuyerEmail, OrderDto orderDto, CancellationToken cancellationToken = default)
        {
            // Get user
            var user = await _authService.GetUserByEmail(BuyerEmail);
            if (user is null)
                throw new BadRequestException("No user found for the authenticated email.");

            //deliver Methods
            var deliveryMethod = await _deliveryMethodsRepo.GetByIdAsync(orderDto.deliveryMethodId);
            if (deliveryMethod is null)
                throw new BadRequestException("No id match Delivery Method !");

            // Get Cart for user from Redis (keyed by the same user id the cart API uses)
            var cart = await _cartRepository.GetCartAsync(user.Id, cancellationToken);
            if (cart is null || cart.Items.Count == 0)
                throw new BadRequestException("The cart is empty or does not exist.");

            // Re-validate every line against the current product (exists, current price/discount).
            // Availability is NOT checked against raw stock here: the quantity was already reserved

            // by the cart. The reservation itself is validated below (ValidateReservationsAsync).
            var orderItems = await BuildOrderItemsAsync(cart, cancellationToken);

            var reservations = await _reservationService.GetActiveReservationsAsync(user.Id, cancellationToken);
            ValidateReservations(orderItems, reservations);

            // shipping Address
            TblShippingAddress ship = _mapper.Map<TblShippingAddress>(orderDto.shipAddress);

            //SubTotal
            var subTotal = orderItems.Sum(x => x.Quentity * x.Price);
            //creating Order
            TblOrders ord = new(BuyerEmail, subTotal, ship, deliveryMethod, orderItems);
            ord.CreatedAt = DateTime.UtcNow;
            ord.CreatedBy = user.Id;

            var res = await _orderRepository.AddAsync(ord);
            if (res is null)
                throw new BadRequestException("some erroe happen");

            // Bind the cart-owned (Pending) reservations to the new order. They are tracked in
            // the same DbContext, so order insert + reservation transition persist in the single
            // IUnitOfWork.Commit that follows. The required RowVersion was loaded with the entity:
            // if the expiration job flipped the reservation in between, SaveChanges throws a
            // DbUpdateConcurrencyException -> 409 and the whole request rolls back cleanly.
            AttachReservationsToOrder(res, orderItems, reservations);

            // NOTE: the order is only tracked here. Persistence happens when the caller commits
            // (IUnitOfWork.Commit), and the Redis cart is cleared afterwards by ClearUserCartAsync
            // so a failed save can never wipe the basket.
            return res;

        }

        public async Task ClearUserCartAsync(string BuyerEmail, CancellationToken cancellationToken = default)
        {
            var user = await _authService.GetUserByEmail(BuyerEmail);
            if (user is null)
                return;
            await _cartRepository.DeleteCartAsync(user.Id, cancellationToken);
        }

        public async Task<List<OrderResponse>> GetAllOrdersForUserAsync(string BuyerEmail, CancellationToken cancellationToken = default)
        {
            List<OrderResponse> result = new();
            //Get Order
            List<TblOrders> orders = await _orderRepository.GetUserOrdersByEmail(BuyerEmail, cancellationToken);//include DeliveryMethod
            foreach (var order in orders)
            {
                // A fresh response per order: reusing one instance made every entry show the last order.
                var oneOrderResponse = new OrderResponse
                {
                    shipAddress = _mapper.Map<shipAddressDto>(order.shippingAddress),
                    SubTotal = order.SubTotal,
                    OrderId = order.Id,
                    CreatedAt = order.CreatedAt,
                    deliveryMethodId = order.deliveryMethod.Id,
                    items = order.orderItems.ToList(),
                    Total = order.GetTotal(),
                    Status = order.status.ToString()
                };
                result.Add(oneOrderResponse);
            }
            //return
            return result;

        }

        public async Task<TblOrders> GetOrderById(int OrderId, CancellationToken cancellationToken = default)
        {
            TblOrders? ord = await _orderRepository.GeOrdersWithMethodDeliveryById(OrderId, cancellationToken);
            if (ord is null)
                throw new BadRequestException("no id related with this order !");
            return ord;
        }

        public async Task<DeliveryMethodResponse> GetDeliveryMethodAsync(string BuyerEmail, int OrderId, CancellationToken cancellationToken = default)
        {
            var order = await _orderRepository.GeOrdersWithMethodDeliveryById(OrderId, cancellationToken);

            if (order is null || !string.Equals(order.BuyerEmail, BuyerEmail, StringComparison.OrdinalIgnoreCase))
                throw new NotFoundException("no id related with this order !");

            return new DeliveryMethodResponse
            {
                id = order.deliveryMethod.Id,
                name = order.deliveryMethod.Name,
                price = order.deliveryMethod.Price
            };//id with name of Delivery Method!
        }

        public async Task<List<DeliveryMethodResponse>> GetAllDeliveryMethodAsync(CancellationToken cancellationToken = default)
        {
            var x = await _deliveryMethodsRepo.GetAllAsync();
            List<DeliveryMethodResponse> res = x.Select(x =>
                           new DeliveryMethodResponse
                           {
                               price = x.Price,
                               id = x.Id,
                               name = x.Name
                           }
            ).ToList();
            return res;

        }

        public async Task<TblOrders> UpdateOrderToConfirm(int orderId)
        {
            var order = await _orderRepository.GetByIdOrThrowAsync(orderId);
            order.status = Status.PaymentReceived;
            return order;
        }

        //Helpers--- 
        private async Task<List<TblOrderItem>> BuildOrderItemsAsync(Cart cart, CancellationToken cancellationToken)
        {
            List<TblOrderItem> res = new();
            foreach (var item in cart.Items)
            {

                var product = await _productService.GetProductForAddToCartAsync(item.ProductId, item.Quantity, cancellationToken, enforceAvailableStock: false);

                var unitPrice = product.PriceExpiresAt.HasValue ? product.FinalPrice : product.BasePrice;

                var orderItem = new TblOrderItem(
                    product.ProductId,
                    product.productImgsUrl?.FirstOrDefault() ?? string.Empty,
                    product.ProductName,
                    unitPrice,
                    item.Quantity);

                res.Add(orderItem);
            }
            return res;
        }

        private static void ValidateReservations(List<TblOrderItem> orderItems, IReadOnlyList<TblStockReservation> reservations)
        {
            foreach (var orderItem in orderItems)
            {
                var reservation = reservations.FirstOrDefault(r => r.ProductId == orderItem.ProductItemId);

                if (reservation is null || reservation.Quantity < orderItem.Quentity)
                {
                    throw new BadRequestException(
                        $"There is not enough reserved quantity for '{orderItem.ProductName}'. Please update your cart and try again.");
                }
            }
        }

        private static void AttachReservationsToOrder(TblOrders order, List<TblOrderItem> orderItems, IReadOnlyList<TblStockReservation> reservations)
        {
            foreach (var orderItem in orderItems)
            {
                var reservation = reservations.FirstOrDefault(r => r.ProductId == orderItem.ProductItemId);
                if (reservation is null || reservation.Status != ReservationStatus.Pending)
                {
                    continue;
                }

                reservation.Status = ReservationStatus.AttachedToOrder;
                reservation.Order = order;
                reservation.Quantity = orderItem.Quentity;
                reservation.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
