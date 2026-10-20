using Ecom.Core.DTO.CartDto;
using Ecom.Core.DTO.CartDto.CartModels;
using Ecom.Core.Exceptions;
using Ecom.Core.Interfaces;
using Ecom.Core.Services;
using Ecom.Core.Services.Facads;

namespace ECom.Infrastructure.Application.Facades
{
    public class CartProductFacade : ICartProductFacade
    {
        private readonly IProductService _productService;
        private readonly ICartRepository _cartRepository;
        private readonly IStockReservationService _reservationService;
        private readonly IReservationJobScheduler _jobScheduler;

        public CartProductFacade(
            ICartRepository cartRepository,
            IProductService productService,
            IStockReservationService reservationService,
            IReservationJobScheduler jobScheduler)
        {
            _cartRepository = cartRepository;
            _productService = productService;
            _reservationService = reservationService;
            _jobScheduler = jobScheduler;
        }

        public async Task<Cart> GetCart(string userId, CancellationToken cancellationToken = default)
        {
            return await _cartRepository.GetCartAsync(userId, cancellationToken);
        }

        public async Task UpdateCartIdForGuestUsers(string cartGuestId, string userId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(cartGuestId)
                || string.IsNullOrWhiteSpace(userId)
                || string.Equals(cartGuestId, userId, StringComparison.Ordinal))
            {
                return;
            }

            var guestCart = await _cartRepository.GetCartAsync(cartGuestId, cancellationToken);
            if (guestCart is null || guestCart.Items.Count == 0)
            {
                // Nothing to move; drop any empty guest key so it cannot linger.
                if (guestCart is not null)
                {
                    await _cartRepository.DeleteCartAsync(cartGuestId, cancellationToken);
                }
                return;
            }

            var userCart = await _cartRepository.GetCartAsync(userId, cancellationToken)
                           ?? new Cart { UserId = userId, Items = new List<CartItem>() };

            // Merge guest lines into the user's basket, summing duplicates so the same product
            // never appears twice.
            foreach (var guestItem in guestCart.Items)
            {
                var existingItem = userCart.Items.FirstOrDefault(x => x.ProductId == guestItem.ProductId);
                if (existingItem is null)
                {
                    userCart.Items.Add(new CartItem
                    {
                        ProductId = guestItem.ProductId,
                        ProductName = guestItem.ProductName,
                        ProductDescription = guestItem.ProductDescription,
                        productImgsUrl = guestItem.productImgsUrl ?? new List<string>(),
                        Quantity = guestItem.Quantity,
                        UnitPrice = guestItem.UnitPrice,
                        PriceExpiresAt = guestItem.PriceExpiresAt
                    });
                }
                else
                {
                    existingItem.Quantity += guestItem.Quantity;
                    existingItem.UnitPrice = guestItem.UnitPrice;
                    existingItem.PriceExpiresAt = guestItem.PriceExpiresAt;
                }
            }

            // Move the stock reservations from the guest id to the authenticated id. The guest
            // reservation is released first (returning the stock) and the merged target is then
            // reserved under the user id, so an already-present line is not double-reserved and
            // the merged quantity stays within what was reserved.
            foreach (var line in userCart.Items)
            {
                int guestReserved = await _reservationService.GetReservedQuantityAsync(cartGuestId, line.ProductId, cancellationToken);
                if (guestReserved > 0)
                {
                    await _reservationService.AdjustReservationAsync(cartGuestId, line.ProductId, -guestReserved, cancellationToken);
                }

                int userReserved = await _reservationService.GetReservedQuantityAsync(userId, line.ProductId, cancellationToken);
                int delta = line.Quantity - userReserved;
                if (delta > 0)
                {
                    await AdjustAndScheduleAsync(userId, line.ProductId, delta, cancellationToken);
                }
                else if (delta < 0)
                {
                    await _reservationService.AdjustReservationAsync(userId, line.ProductId, delta, cancellationToken);
                }
            }

            userCart.UserId = userId;

            var result = await _cartRepository.SetCartAsync(userId, userCart);
            if (!result)
            {
                throw new BadRequestException("An error occurred while merging the guest cart.");
            }

            await _cartRepository.DeleteCartAsync(cartGuestId, cancellationToken);
        }

        public async Task<Cart> AddProductToCartAsync(string userId, int productId, int quantity, CancellationToken cancellationToken = default)
        {
            if (quantity <= 0)
            {
                throw new BadRequestException("Quantity must be greater than zero.");
            }

            var cart = await _cartRepository.GetCartAsync(userId, cancellationToken);
            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = userId,
                    Items = new List<CartItem>()
                };
            }

            var existingItem = cart.Items
                .FirstOrDefault(x => x.ProductId == productId);

            int newQuantity = quantity + (existingItem?.Quantity ?? 0);

            // Price/name read only; stock is enforced atomically by the reservation adjust below.
            var product = await _productService
                .GetProductForAddToCartAsync(productId, newQuantity, cancellationToken, enforceAvailableStock: false);

            // The reservation is the difference between what the line needs and what is
            // already reserved for this user, so re-adding is never double-reserved and an
            // expired reservation is re-created automatically.
            int reservedQuantity = await _reservationService.GetReservedQuantityAsync(userId, productId, cancellationToken);
            int delta = newQuantity - reservedQuantity;

            if (delta > 0)
            {
                await AdjustAndScheduleAsync(userId, productId, delta, cancellationToken);
            }
            else if (delta < 0)
            {
                await _reservationService.AdjustReservationAsync(userId, productId, delta, cancellationToken);
            }

            if (existingItem != null)
            {
                existingItem.Quantity = newQuantity;
                SetLinePrice(existingItem, product);
            }
            else
            {
                cart.Items.Add(new CartItem
                {
                    ProductId = product.ProductId,
                    ProductName = product.ProductName,
                    ProductDescription = product.ProductDescription,
                    productImgsUrl = product.productImgsUrl ?? new List<string>(),
                    Quantity = quantity,
                    UnitPrice = product.PriceExpiresAt.HasValue ? product.FinalPrice : product.BasePrice,
                    PriceExpiresAt = product.PriceExpiresAt
                });
            }

            var result = await _cartRepository.SetCartAsync(
                userId,
                cart);

            if (!result)
            {
                throw new BadRequestException(
                    "An error occurred while adding the product to the cart.");
            }
            return cart;
        }

        public async Task<Cart> UpdateCartItemAsync(   string userId,   int productId,   int quantity,  CancellationToken cancellationToken = default)
        {
            var cart = await _cartRepository.GetCartAsync(userId, cancellationToken);

            if (cart == null)
            {
                throw new NotFoundException("Cart not found.");
            }

            var item = cart.Items
                .FirstOrDefault(x => x.ProductId == productId);

            if (item == null)
            {
                throw new NotFoundException("Product is not in the cart.");
            }

            int newQuantity = Math.Max(quantity, 0);

            ProductCartInfoDto? product = null;
            if (newQuantity > 0)
            {
                // Stock is enforced by the reservation adjust, not this read.
                product = await _productService
                    .GetProductForAddToCartAsync(productId, newQuantity, cancellationToken, enforceAvailableStock: false);
            }

            int reservedQuantity = await _reservationService.GetReservedQuantityAsync(userId, productId, cancellationToken);
            int delta = newQuantity - reservedQuantity;

            if (delta > 0)
            {
                await AdjustAndScheduleAsync(userId, productId, delta, cancellationToken);
            }
            else if (delta < 0)
            {
                // Release only the difference (delta reconciliation), never a full re-reserve.
                await _reservationService.AdjustReservationAsync(userId, productId, delta, cancellationToken);
            }

            if (newQuantity > 0)
            {
                item.Quantity = newQuantity;
                SetLinePrice(item, product!);
            }
            else
            {
                cart.Items.Remove(item);
            }

            var result = await _cartRepository.SetCartAsync(
                userId,
                cart);

            if (!result)
            {
                throw new BadRequestException(
                    "An error occurred while updating the cart.");
            }

            return cart;
        }

        public async Task DeleteCartAsync(string userId, CancellationToken cancellationToken = default)
        {
            var cart = await _cartRepository.GetCartAsync(userId, CancellationToken.None);
            if (cart == null)
            {
                throw new BadRequestException("no Cart for this userID");
            }

            // Restore every cart-owned (Pending) reservation exactly once before dropping the basket.
            foreach (var item in cart.Items)
            {
                int reservedQuantity = await _reservationService
                    .GetReservedQuantityAsync(userId, item.ProductId, CancellationToken.None);
                if (reservedQuantity > 0)
                {
                    await _reservationService.AdjustReservationAsync(
                        userId,
                        item.ProductId,
                        -reservedQuantity,
                        CancellationToken.None);
                }
            }

            bool res = await _cartRepository.DeleteCartAsync(userId, cancellationToken);
            if (res == false)
            {
                throw new BadRequestException("no Cart for this userID");
            }
        }

        public async Task<Cart> UpdateCartAsync(UpdateCartRequest request, CancellationToken cancellationToken = default)
        {
            string userId = request.userId ?? string.Empty;
            var cart = await _cartRepository.GetCartAsync(userId, cancellationToken);

            if (cart == null)
            {
                throw new NotFoundException("Cart not found.");
            }

            var requestedByProduct = request.Items
                .GroupBy(x => x.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            // Update endpoint cannot add new products
            foreach (var item in request.Items)
            {
                if (!cart.Items.Any(x => x.ProductId == item.ProductId))
                {
                    throw new BadRequestException(
                        $"Product {item.ProductId} does not exist in the cart.");
                }
            }

            // Reconcile cart lines + requested lines + silent removals (omitted products).
            // Materialized BEFORE the loop because the loop removes lines from cart.Items.
            var productIds = cart.Items
                .Select(i => i.ProductId)
                .Union(requestedByProduct.Keys)
                .ToList();

            foreach (var productId in productIds)
            {
                int newQuantity = requestedByProduct.TryGetValue(productId, out var wanted)
                    ? Math.Max(wanted, 0)
                    : 0;

                ProductCartInfoDto? product = null;
                if (newQuantity > 0)
                {
                    product = await _productService
                        .GetProductForAddToCartAsync(productId, newQuantity, cancellationToken, enforceAvailableStock: false);
                }

                int reservedQuantity = await _reservationService.GetReservedQuantityAsync(userId, productId, cancellationToken);
                int delta = newQuantity - reservedQuantity;

                if (delta > 0)
                {
                    await AdjustAndScheduleAsync(userId, productId, delta, cancellationToken);
                }
                else if (delta < 0)
                {
                    await _reservationService.AdjustReservationAsync(userId, productId, delta, cancellationToken);
                }

                var cartItem = cart.Items
                    .FirstOrDefault(x => x.ProductId == productId);

                if (newQuantity > 0 && cartItem != null)
                {
                    cartItem.Quantity = newQuantity;
                    SetLinePrice(cartItem, product!);
                }
                else if (cartItem != null)
                {
                    cart.Items.Remove(cartItem);
                }
            }

            var result = await _cartRepository.SetCartAsync(
                userId,
                cart);

            if (!result)
            {
                throw new BadRequestException(
                    "An error occurred while updating the cart.");
            }

            return cart;
        }

        private async Task AdjustAndScheduleAsync(string userId, int productId, int deltaQuantity, CancellationToken cancellationToken)
        {
            var result = await _reservationService.AdjustReservationAsync(userId, productId, deltaQuantity, cancellationToken);

            // Only freshly created reservations get a 3-day Hangfire expiration job;
            // renewed/extended reservations keep their original job (which no-ops when the
            // ExpiresAt was refreshed past its due time).
            if (result.NewlyCreated && result.Changed)
            {
                _jobScheduler.ScheduleExpiration(result.ReservationId, result.ExpiresAtUtc);
            }
        }

        private static void SetLinePrice(CartItem item, ProductCartInfoDto product)
        {
            item.UnitPrice = product.PriceExpiresAt.HasValue ? product.FinalPrice : product.BasePrice;
            item.PriceExpiresAt = product.PriceExpiresAt;
        }
    }
}