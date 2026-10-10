using ECom.Api.DTO;
using FluentValidation;

namespace ECom.Api.Validators
{
    public class AddProductFormValidator : AbstractValidator<AddProductFormDto>
    {
        public AddProductFormValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Product name is required.");

            RuleFor(x => x.Description)
                .NotEmpty().WithMessage("Product description is required.");

            RuleFor(x => x.Price)
                .GreaterThanOrEqualTo(0).WithMessage("Price must be greater than or equal to 0.");

            RuleFor(x => x.CategoryId)
                .GreaterThan(0).WithMessage("Category id must be greater than 0.");

            When(x => HasAnyDiscountField(x), () =>
            {
                RuleFor(x => x.DiscountFrom)
                    .NotEmpty().WithMessage("Discount start date is required.");

                RuleFor(x => x.DiscountTo)
                    .NotEmpty().WithMessage("Discount end date is required.");

                RuleFor(x => x.DiscountType)
                    .NotNull().WithMessage("Discount type is required.")
                    .IsInEnum().WithMessage("Discount type is invalid.");

                RuleFor(x => x.DiscountValue)
                    .NotNull().WithMessage("Discount value is required.")
                    .GreaterThan(0).WithMessage("Discount value must be greater than 0.");

                RuleFor(x => x.DiscountFrom)
                    .Must((form, from) => !from.HasValue || !form.DiscountTo.HasValue || from.Value < form.DiscountTo.Value)
                    .WithMessage("Discount start date must be before the end date.");
            });
        }

        private static bool HasAnyDiscountField(AddProductFormDto form)
        {
            return form.DiscountFrom.HasValue
                || form.DiscountTo.HasValue
                || form.DiscountType.HasValue
                || form.DiscountValue.HasValue
                || !string.IsNullOrWhiteSpace(form.DiscountName);
        }
    }
}