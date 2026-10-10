using ECom.Api.DTO;
using FluentValidation;

namespace ECom.Api.Validators
{
    public class AddCategoryFormValidator : AbstractValidator<AddCategoryFormDto>
    {
        public AddCategoryFormValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Category name is required.");

            RuleFor(x => x.Description)
                .NotEmpty().WithMessage("Category description is required.");
        }
    }
}