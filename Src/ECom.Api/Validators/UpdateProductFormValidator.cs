using ECom.Api.DTO;
using FluentValidation;

namespace ECom.Api.Validators
{
    public class UpdateProductFormValidator : AbstractValidator<UpdateProductFormDto>
    {
        public UpdateProductFormValidator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0).WithMessage("Product id must be greater than 0.");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Product name is required.");

            RuleFor(x => x.Description)
                .NotEmpty().WithMessage("Product description is required.");

            RuleFor(x => x.Price)
                .GreaterThanOrEqualTo(0).WithMessage("Price must be greater than or equal to 0.");

            RuleFor(x => x.Quantity)
                .GreaterThanOrEqualTo(0).WithMessage("Quantity must be greater than or equal to 0.");

            RuleFor(x => x.CategoryId)
                .GreaterThan(0).WithMessage("Category id must be greater than 0.");
        }
    }
}