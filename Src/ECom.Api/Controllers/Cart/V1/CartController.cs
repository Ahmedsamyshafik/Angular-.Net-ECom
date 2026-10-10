using Asp.Versioning;

using Ecom.Core.DTO.CartDto;
using Ecom.Core.Interfaces;

using ECom.Api.DTO;
using ECom.Api.Helper;

using Microsoft.AspNetCore.Mvc;

namespace ECom.Api.Controllers.Cart.V1
{
    //[Route("api/[controller]")]
    [Route("api/v{version:apiVersion}/Cart")]
    [ApiVersion("1.0")]

    [ApiController]
    public class CartController : MyBaseController
    {
        public CartController(IUnitOfWork unitOfWork) : base(unitOfWork)
        {


        }
        [HttpGet("Get")]
        public async Task<IActionResult> Get([FromQuery] string userId, CancellationToken cancellationToken = default)
        {
            var res = await _unitOfWork.CartProductFacade.GetCart(userId, cancellationToken);
            return Ok(new responseApi(200, "s", res));
        }
        [HttpPost("Add")]
        public async Task<IActionResult> AddCart([FromBody] AddProductToCartDto Dto, CancellationToken cancellationToken = default)
        {
            var res = await _unitOfWork.CartProductFacade
                .AddProductToCartAsync(Dto.UserId, Dto.productId, Dto.Quentity, cancellationToken);
            return Ok(new responseApi(200, "Success", res));
        }
        [HttpDelete("Delete")]
        public async Task<IActionResult> Delete([FromQuery] string userId)
        {
            await _unitOfWork.CartProductFacade.DeleteCartAsync(userId);

            return Ok(new responseApi(200, "Success", null));
        }
        [HttpPatch("update")]
        public async Task<IActionResult> Update([FromBody] UpdateCartItemDto dto, CancellationToken cancellationToken = default)
        {
            Ecom.Core.CartModels.Cart? res = await _unitOfWork.CartProductFacade.UpdateCartItemAsync(dto.userId, dto.productId, dto.quantity, cancellationToken);
            return Ok(new responseApi(200, "success", res));
        }

        [HttpPatch("UpdateCart")]

        public async Task<IActionResult> UpdateCart(UpdateCartRequest req, CancellationToken cancellationToken = default)
        {
            var cart = await _unitOfWork.CartProductFacade.UpdateCartAsync(req, cancellationToken);
            return Ok(new responseApi(200, "success", cart));
        }
    }
}
