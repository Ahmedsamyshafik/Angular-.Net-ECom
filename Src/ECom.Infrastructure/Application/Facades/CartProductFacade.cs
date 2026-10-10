using Ecom.Core.CartModels;
using Ecom.Core.DTO.CartDto;
using Ecom.Core.Exceptions;
using Ecom.Core.Interfaces;
using Ecom.Core.Services;

namespace ECom.Infrastructure.Application.Facades
{
    public class CartProductFacade : ICartProductFacade
    {
        private readonly IProductService _productService;
        private readonly ICartRepository _cartRepository;

        public CartProductFacade(ICartRepository cartRepository, IProductService productService)
        {
            _cartRepository = cartRepository;
            _productService = productService;
        }
        public async Task<Cart> GetCart(string userId, CancellationToken cancellationToken)
        {
            return await _cartRepository.GetCartAsync(userId, cancellationToken);
        }

        public async Task<Cart> AddProductToCartAsync(string userId, int productId, int quantity, CancellationToken cancellationToken)
        {
            string cartId = "getting from user id ";

            var product = await _productService
                .GetProductForAddToCartAsync(
                    productId,
                    quantity,
                    cancellationToken);

            if (product == null)
                throw new NotFoundException("Product not found.");


            CartItem item = new CartItem
            {
                ProductId = product.ProductId,
                Quantity = quantity
            };
            // discount Handle
            if (product.PriceExpiresAt.HasValue)
            {
                item.PriceExpiresAt = product.PriceExpiresAt;
                item.UnitPrice = product.FinalPrice;
            }
            else
            {
                item.UnitPrice = product.BasePrice;
            }

            var cart = await _cartRepository.GetCartAsync(cartId, cancellationToken);
            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = userId,
                    Items = new List<CartItem>()
                };
            }
            //when add auth we will check if this cart belong to the userId or not!
            var existingItem = cart.Items
                .FirstOrDefault(x => x.ProductId == product.ProductId);
            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
                existingItem.UnitPrice = item.UnitPrice;
                existingItem.PriceExpiresAt = item.PriceExpiresAt;
            }
            else
            {
                cart.Items.Add(item);
            }


            var result = await _cartRepository.SetCartAsync(
                cartId,
                cart);

            if (!result)
            {
                throw new BadRequestException(
                    "An error occurred while adding the product to the cart.");
            }
            return cart;

        }
        public async Task<Cart> UpdateCartItemAsync(
      string userId,
      int productId,
      int quantity,
      CancellationToken cancellationToken)
        {
            userId = "12";
            string cartId = "12";
            var prod = await _productService.GetByIdAsync(productId); ;


            var cart = await _cartRepository.GetCartAsync(cartId, cancellationToken);

            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = userId,
                    Items = new List<CartItem>()
                };
            }


            var item = cart.Items
                .FirstOrDefault(x => x.ProductId == productId);

            //if (item == null)
            //    throw new NotFoundException("Product is not in the cart.");
            //if the product in the cart before!
            if (item != null)
            {
                item.Quantity++;
            }
            else
            {
                //check if offer
                decimal price = 0;
                DateTime? ExpProd = default;
                var discountCheck = await _productService.GetProductForAddToCartAsync(productId, quantity);
                if (discountCheck.PriceExpiresAt != null || discountCheck.FinalPrice != 0)
                {
                    price = discountCheck.FinalPrice;
                    ExpProd = discountCheck.PriceExpiresAt;
                }
                else
                {
                    price = discountCheck.BasePrice;
                }
                cart.Items.Add(new CartItem
                {
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPrice = price,
                    PriceExpiresAt = ExpProd,
                    ProductDescription = prod.Description,
                    productImgsUrl = prod.PhotoUrls?.Select(x => x.Url).ToList(),
                    ProductName = prod.Name

                });
            }


            var result = await _cartRepository.SetCartAsync(
                cartId,
                cart);

            if (!result)
            {
                throw new BadRequestException(
                    "An error occurred while updating the cart.");
            }

            return cart;
        }


        public async Task DeleteCartAsync(string userId)
        {
            bool res = await _cartRepository.DeleteCartAsync(userId);
            if (res == false)
            {
                throw new BadRequestException("no Cart for this userID");

            }
        }


        public async Task<Cart> UpdateCartAsync(UpdateCartRequest request, CancellationToken cancellationToken)
        {
            request.userId = "12";
            var cart = await _cartRepository.GetCartAsync(request.userId, cancellationToken);

            if (cart == null)
            {
                throw new NotFoundException("Cart not found.");
            }

            foreach (var item in request.Items)
            {
                var cartItem = cart.Items
                    .FirstOrDefault(x => x.ProductId == item.ProductId);

                // Update endpoint cannot add new products
                if (cartItem == null)
                {
                    throw new BadRequestException(
                        $"Product {item.ProductId} does not exist in the cart.");
                }

                var product = await _productService
                    .GetProductForAddToCartAsync(
                        item.ProductId,
                        item.Quantity,
                        cancellationToken);

                cartItem.Quantity = item.Quantity;

                if (product.PriceExpiresAt.HasValue)
                {
                    cartItem.PriceExpiresAt = product.PriceExpiresAt;
                    cartItem.UnitPrice = product.FinalPrice;
                }
                else
                {
                    cartItem.PriceExpiresAt = null;
                    cartItem.UnitPrice = product.BasePrice;
                }

                //cartItem.Total = cartItem.Quantity * cartItem.UnitPrice;
            }

            var requestedProductIds = request.Items
                .Select(x => x.ProductId)
                .ToHashSet();

            cart.Items.RemoveAll(x =>
                !requestedProductIds.Contains(x.ProductId));
            var x = cart.Items.Sum(x => x.Total);
            //cart.Total = cart.Items.Sum(x => x.Total);

            var result = await _cartRepository.SetCartAsync(
                request.userId,
                cart);

            if (!result)
            {
                throw new BadRequestException(
                    "An error occurred while updating the cart.");
            }

            return cart;
        }
    }
}
