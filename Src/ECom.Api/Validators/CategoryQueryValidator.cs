using Ecom.Core.DTO.Categories;
using FluentValidation;

namespace ECom.Api.Validators
{
    public class CategoryQueryValidator : AbstractValidator<CategoryQueryParams>
    {
        public CategoryQueryValidator()
        {
            RuleFor(x => x.PageNumber)
                .GreaterThanOrEqualTo(1).WithMessage("Page number must be greater than zero.");

            RuleFor(x => x.PageSize)
                .GreaterThanOrEqualTo(1).WithMessage("Page size must be greater than zero.");
        }
    }
}