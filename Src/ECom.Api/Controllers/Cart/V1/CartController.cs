using System.Security.Claims;

using Asp.Versioning;

using Ecom.Core.DTO.CartDto;
using Ecom.Core.Interfaces;

using ECom.Api.DTO;
using ECom.Api.Helper;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECom.Api.Controllers.Cart.V1
{
    //[Route("api/[controller]")]
    [Route("api/v{version:apiVersion}/Cart")]
    [ApiVersion("1.0")]

    [ApiController]
    // Guest shopping is intentional: the cart must stay reachable without authentication.
    // The owner id is always derived server-side (auth claim or server-issued guest cookie),
    // never from the request, so anonymous access cannot reach another user's basket.
    [AllowAnonymous]
    public class CartController : MyBaseController
    {
        public CartController(IUnitOfWork unitOfWork) : base(unitOfWork)
        {


        }

        // The cart owner is the authenticated user id when signed in, otherwise the stable
        // server-issued guest id from the cookie. A fresh Guid per request (the previous
        // behavior) made guest carts impossible to retrieve.
        private string cardId
        {
            get
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    return userId;
                }

                return GuestCartCookie.Resolve(HttpContext);
            }
        }

        [HttpGet("Get")]
        public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
        {

            Ecom.Core.DTO.CartDto.CartModels.Cart? res = await _unitOfWork.CartProductFacade.GetCart(cardId, cancellationToken);
            return Ok(new responseApi(200, "s", res));
        }
        [HttpPost("Add")]
        public async Task<IActionResult> AddCart([FromBody] AddProductToCartDto Dto, CancellationToken cancellationToken = default)
        {
            var res = await _unitOfWork.CartProductFacade
                .AddProductToCartAsync(cardId, Dto.productId, Dto.Quentity, cancellationToken);
            return Ok(new responseApi(200, "Success", res));
        }
        [HttpDelete("Delete")]
        public async Task<IActionResult> Delete(CancellationToken cancellationToken = default)
        {
            await _unitOfWork.CartProductFacade.DeleteCartAsync(cardId, cancellationToken);

            return Ok(new responseApi(200, "Success", null));
        }
        [HttpPatch("update")]
        public async Task<IActionResult> Update([FromBody] UpdateCartItemDto dto, CancellationToken cancellationToken = default)
        {
            Ecom.Core.DTO.CartDto.CartModels.Cart? res = await _unitOfWork.CartProductFacade.UpdateCartItemAsync(cardId, dto.productId, dto.quantity, cancellationToken);
            return Ok(new responseApi(200, "success", res));
        }

        //deperacted
        [HttpPatch("UpdateCart")]

        public async Task<IActionResult> UpdateCart(UpdateCartRequest req, CancellationToken cancellationToken = default)
        {
            req.userId = cardId;
            var cart = await _unitOfWork.CartProductFacade.UpdateCartAsync(req, cancellationToken);
            return Ok(new responseApi(200, "success", cart));
        }
    }
}
