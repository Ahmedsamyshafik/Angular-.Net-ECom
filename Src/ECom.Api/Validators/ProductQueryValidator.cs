using Ecom.Core.DTO.Products;
using FluentValidation;

namespace ECom.Api.Validators
{
    public class ProductQueryValidator : AbstractValidator<ProductQueryParams>
    {
        public ProductQueryValidator()
        {
            RuleFor(x => x.PageNumber)
                .GreaterThanOrEqualTo(1).WithMessage("Page number must be greater than zero.");

            RuleFor(x => x.PageSize)
                .GreaterThanOrEqualTo(1).WithMessage("Page size must be greater than zero.");
        }
    }
}