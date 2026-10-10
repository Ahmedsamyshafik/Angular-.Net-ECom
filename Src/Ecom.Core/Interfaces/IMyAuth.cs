using Ecom.Core.DTO.UserDto;

using ECom.Infrastructure.Repositories;

namespace Ecom.Core.Interfaces
{
    public interface IMyAuth
    {
        Task<string> RegisterAsync(RegisterDto registerDto);
        Task SendEmailAsync(string email, string code, string component, string subject, string message);
        Task<UserWithTokenDto> Login(LoginDto loginDto);

        Task<bool> SendEmailForForgetPassword(string email);
        Task<string> ResetPassword(ResetPasswordDto resetPasswordDto);
        Task<bool> ActiveAccount(ActiveAccountDto activeAccountDto);
        Task<string> ChangePassword(ChangePasswordDto changePasswordDto);


    }
}
