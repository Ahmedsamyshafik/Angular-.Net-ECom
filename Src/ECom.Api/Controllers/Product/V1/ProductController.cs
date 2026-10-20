using Asp.Versioning;

using Ecom.Core.Constants;
using Ecom.Core.DTO.Products;
using Ecom.Core.Interfaces;

using ECom.Api.DTO;
using ECom.Api.Helper;
using ECom.Api.Registerations;

using FluentValidation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECom.Api.Controllers.Product.V1
{

    [Route("api/v{version:apiVersion}/product")]
    [ApiController]
    [ApiVersion("1.0")]
    [ApiVersion("2.0")]
    [EnableRateLimiting(RateLimitingRegisteration.PerClientPolicyName)]
    public class ProductController : MyBaseController
    {
        #region Private Fields

        private readonly IValidator<AddProductFormDto> _productValidator;
        private readonly IValidator<UpdateProductFormDto> _updateProductValidator;
        private readonly IValidator<AddDiscountDto> _discountValidator;
        private readonly IValidator<ProductQueryParams> _queryValidator;

        #endregion

        #region Constructors

        public ProductController(
            IUnitOfWork unitOfWork,
            IValidator<AddProductFormDto> productValidator,
            IValidator<UpdateProductFormDto> updateProductValidator,
            IValidator<AddDiscountDto> discountValidator,
            IValidator<ProductQueryParams> queryValidator)
            : base(unitOfWork)
        {
            _productValidator = productValidator;
            _updateProductValidator = updateProductValidator;
            _discountValidator = discountValidator;
            _queryValidator = queryValidator;
        }

        #endregion

        #region Methods

        [HttpGet("GetAll")]
        [MapToApiVersion("1.0")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllV1([FromQuery] ProductQueryParams? query, CancellationToken cancellationToken = default)
        {
            query ??= new ProductQueryParams();
            await ValidationHelper.ThrowIfInvalidAsync(_queryValidator, query, cancellationToken);

            var result = await _unitOfWork.ProductService.GetPageAsync(query, cancellationToken);

            return Ok(new responseApi(200, "Products found.", result));
        }
    

        [HttpGet("GetById/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken = default)
        {
            var product = await _unitOfWork.ProductService.GetByIdAsync(id, cancellationToken);

            return Ok(new responseApi(200, "Product found.", product));
        }

        // Catalog writes are administrator-only: [Authorize] alone would allow any signed-in
        // shopper to mutate the catalog.
        [HttpPost("Add")]
        [Authorize(Roles = AppRoles.Admin)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Add([FromForm] AddProductFormDto form, CancellationToken cancellationToken = default)
        {
            await ValidationHelper.ThrowIfInvalidAsync(_productValidator, form, cancellationToken);

            var result = await _unitOfWork.AddProductOrchestrator.ExecuteAsync(form.ToDto(), SystemUserId, cancellationToken);

            try
            {
                await _unitOfWork.Commit(cancellationToken);
            }
            catch
            {
                await _unitOfWork.AddProductOrchestrator.CompensateAsync(result, cancellationToken);
                throw;
            }

            return Ok(new responseApi(200, "Product added.", result.Product));
        }

        [HttpPut("Update")]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> Update([FromForm] UpdateProductFormDto form, CancellationToken cancellationToken = default)
        {
            await ValidationHelper.ThrowIfInvalidAsync(_updateProductValidator, form, cancellationToken);

            var product = await _unitOfWork.ProductService.UpdateAsync(form.ToDto(), SystemUserId, cancellationToken);
            await _unitOfWork.Commit(cancellationToken);

            return Ok(new responseApi(200, "Product updated.", product));
        }

        [HttpDelete("Delete/{id}")]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
        {
            await _unitOfWork.ProductService.DeleteAsync(id, SystemUserId, cancellationToken);
            await _unitOfWork.Commit(cancellationToken);

            return Ok(new responseApi(200, "Product deleted."));
        }

        [HttpPost("ApplyDiscount")]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> ApplyDiscount([FromBody] AddDiscountDto discount, CancellationToken cancellationToken = default)
        {
            await ValidationHelper.ThrowIfInvalidAsync(_discountValidator, discount, cancellationToken);

            await _unitOfWork.DiscountFacade.ApplyDiscountAsync(discount, cancellationToken);
            await _unitOfWork.Commit(cancellationToken);

            return Ok(new responseApi(200, "Discount applied."));
        }

        [HttpPost("ApplyCategoryDiscount")]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> ApplyCategoryDiscount([FromBody] AddDiscountDto discount, CancellationToken cancellationToken = default)
        {
            await ValidationHelper.ThrowIfInvalidAsync(_discountValidator, discount, cancellationToken);

            await _unitOfWork.DiscountFacade.ApplyCategoryDiscountAsync(discount, cancellationToken);
            await _unitOfWork.Commit(cancellationToken);

            return Ok(new responseApi(200, "Category discount applied to " + (discount.ProductIds?.Count ?? 0) + " products."));
        }

        #endregion
    }
}
