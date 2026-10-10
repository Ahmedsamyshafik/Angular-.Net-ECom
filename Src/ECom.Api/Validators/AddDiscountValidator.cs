using Ecom.Core.DTO.Products;
using FluentValidation;

namespace ECom.Api.Validators
{
    public class AddDiscountValidator : AbstractValidator<AddDiscountDto>
    {
        public AddDiscountValidator()
        {
            RuleFor(x => x.From)
                .NotEmpty().WithMessage("Discount start date is required.");

            RuleFor(x => x.To)
                .NotEmpty().WithMessage("Discount end date is required.");

            RuleFor(x => x)
                .Must(x => x.From < x.To)
                .WithMessage("Discount start date must be before the end date.");

            RuleFor(x => x.Value)
                .GreaterThan(0).WithMessage("Discount value must be greater than 0.");

            RuleFor(x => x.DiscountType)
                .IsInEnum().WithMessage("Discount type is invalid.");

            RuleFor(x => x.CategoryId)
                .GreaterThan(0)
                .When(x => x.CategoryId.HasValue)
                .WithMessage("Category id must be greater than 0.");

            RuleFor(x => x)
                .Must(x => x.CategoryId.HasValue || (x.ProductIds != null && x.ProductIds.Count > 0))
                .WithMessage("Provide at least one product id or a category id.");
        }
    }
}