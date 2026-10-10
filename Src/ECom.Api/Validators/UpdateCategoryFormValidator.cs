using ECom.Api.DTO;
using FluentValidation;

namespace ECom.Api.Validators
{
    public class UpdateCategoryFormValidator : AbstractValidator<UpdateCategoryFormDto>
    {
        public UpdateCategoryFormValidator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0).WithMessage("Category id must be greater than 0.");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Category name is required.");

            RuleFor(x => x.Description)
                .NotEmpty().WithMessage("Category description is required.");
        }
    }
}