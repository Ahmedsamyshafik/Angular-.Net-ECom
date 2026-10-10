using Asp.Versioning;

using Ecom.Core.DTO.Categories;
using Ecom.Core.Interfaces;

using ECom.Api.Controllers;
using ECom.Api.DTO;
using ECom.Api.Helper;
using ECom.Api.Registerations;

using FluentValidation;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECom.Api.Controllers.Category.V1
{
    // API version comes from the URL: /api/v1/Category/GetAll
    //
    [Route("api/v{version:apiVersion}/Category")]
    [ApiController]
    [ApiVersion("1.0")]
    [EnableRateLimiting(RateLimitingRegisteration.PerClientPolicyName)]
    public class CategoryController : MyBaseController
    {
        #region Private Fields

        private readonly IValidator<AddCategoryFormDto> _addCategoryValidator;
        private readonly IValidator<UpdateCategoryFormDto> _updateCategoryValidator;
        private readonly IValidator<CategoryQueryParams> _queryValidator;

        #endregion

        #region Constructors

        public CategoryController(
            IUnitOfWork unitOfWork,
            IValidator<AddCategoryFormDto> addCategoryValidator,
            IValidator<UpdateCategoryFormDto> updateCategoryValidator,
            IValidator<CategoryQueryParams> queryValidator)
            : base(unitOfWork)
        {
            _addCategoryValidator = addCategoryValidator;
            _updateCategoryValidator = updateCategoryValidator;
            _queryValidator = queryValidator;
        }

        #endregion

        #region Methods

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] CategoryQueryParams? query, CancellationToken cancellationToken = default)
        {
            query ??= new CategoryQueryParams();
            await ValidationHelper.ThrowIfInvalidAsync(_queryValidator, query, cancellationToken);

            var result = await _unitOfWork.CategoryService.GetPageAsync(query, cancellationToken);

            return Ok(new responseApi(200, "Categories retrieved successfully", result));
        }

        [HttpGet("GetById/{id}")]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken = default)
        {
            var category = await _unitOfWork.CategoryService.GetByIdAsync(id, cancellationToken);

            return Ok(new responseApi(200, "Category found.", category));
        }

        [HttpPost("AddCategory")]
        public async Task<IActionResult> Add([FromForm] AddCategoryFormDto form, CancellationToken cancellationToken = default)
        {
            await ValidationHelper.ThrowIfInvalidAsync(_addCategoryValidator, form, cancellationToken);

            var result = await _unitOfWork.CategoryService.AddAsync(form.ToDto(), cancellationToken);

            try
            {
                await _unitOfWork.Commit(cancellationToken);
            }
            catch
            {
                await _unitOfWork.CategoryService.CompensateAsync(result, cancellationToken);
                throw;
            }

            return Ok(new responseApi(200, "Category added successfully", result.Category));
        }

        [HttpPut("UpdateCategory")]
        public async Task<IActionResult> Update([FromForm] UpdateCategoryFormDto form, CancellationToken cancellationToken = default)
        {
            await ValidationHelper.ThrowIfInvalidAsync(_updateCategoryValidator, form, cancellationToken);

            var category = await _unitOfWork.CategoryService.UpdateAsync(form.ToDto(), cancellationToken);
            await _unitOfWork.Commit(cancellationToken);

            return Ok(new responseApi(200, "Category updated successfully", category));
        }

        [HttpDelete("DeleteCategory/{id}")]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
        {
            await _unitOfWork.CategoryService.DeleteAsync(id, cancellationToken);
            await _unitOfWork.Commit(cancellationToken);

            return Ok(new responseApi(200, "Category deleted successfully"));
        }

        #endregion
    }
}
