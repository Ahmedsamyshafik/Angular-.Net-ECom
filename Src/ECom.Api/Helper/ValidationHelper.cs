using Ecom.Core.Exceptions;
using FluentValidation;
using FluentValidation.Results;

namespace ECom.Api.Helper
{
    public static class ValidationHelper
    {
        public static async Task ThrowIfInvalidAsync<T>(
            IValidator<T> validator,
            T instance,
            CancellationToken cancellationToken = default)
        {
            if (validator == null)
            {
                return;
            }

            ValidationResult result = await validator.ValidateAsync(instance, cancellationToken);
            if (!result.IsValid)
            {
                string message = string.Join(" ", result.Errors.Select(e => e.ErrorMessage));
                throw new BadRequestException(message);
            }
        }
    }
}