using Ecom.Core.DTO.UserDto;
using Ecom.Core.Entities.User;

using ECom.Infrastructure.Repositories;

namespace Ecom.Core.Interfaces
{
    public interface IMyAuth
    {
        Task<string> RegisterAsync(RegisterDto registerDto, CancellationToken cancellationToken = default);
        Task SendEmailAsync(string email, string code, string component, string subject, string message, CancellationToken cancellationToken = default);
        Task<UserWithTokenDto> Login(LoginDto loginDto, CancellationToken cancellationToken = default);

        Task<bool> SendEmailForForgetPassword(string email, CancellationToken cancellationToken = default);
        Task<string> ResetPassword(ResetPasswordDto resetPasswordDto, CancellationToken cancellationToken = default);
        Task<bool> ActiveAccount(ActiveAccountDto activeAccountDto, CancellationToken cancellationToken = default);
        /// <summary>
        /// Changes the password of the user identified by <paramref name="userId"/> (resolved
        /// from the authenticated claims, never from a request-supplied email). The current
        /// password is still verified by Identity and NewPassword/ConfirmPassword must match.
        /// </summary>
        Task<string> ChangePassword(string userId, ChangePasswordDto changePasswordDto, CancellationToken cancellationToken = default);
        Task<AppUser> GetUserByEmail(string email, CancellationToken cancellationToken = default);


    }
}
