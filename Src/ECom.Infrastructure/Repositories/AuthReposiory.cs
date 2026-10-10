using Ecom.Core.DTO.sharing;
using Ecom.Core.DTO.UserDto;
using Ecom.Core.Entities.User;
using Ecom.Core.Exceptions;
using Ecom.Core.Interfaces;
using Ecom.Core.Services;

using Hangfire;

using Microsoft.AspNetCore.Identity;

namespace ECom.Infrastructure.Repositories
{
    public class AuthReposiory : IMyAuth
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IGenerateToken _generateToken;
        public AuthReposiory(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, IGenerateToken generateToken, IEmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _generateToken = generateToken;
            _emailService = emailService;
        }

        public async Task<string> RegisterAsync(RegisterDto registerDto)
        {
            if (registerDto == null)
                return "Faild!";
            //check if email already exist!
            var checkingEmail = await _userManager.FindByEmailAsync(registerDto.Email);
            if (checkingEmail != null)
                throw new BadRequestException("Email Already Exist!");
            //Start Saving data

            AppUser user = new AppUser()
            {
                Email = registerDto.Email,
                UserName = registerDto.userName,
                DisplayName = registerDto.DisplayName
            };
            var res = await _userManager.CreateAsync(user, registerDto.password);
            if (!res.Succeeded)
            {
                return GetIdentityErrors(res.Errors);
                //throw new BadRequestException(res.Errors?.ToList().Select(x => x.Description)?.ToString());
            }
            //Send Active token to Email

            //using Hangfire ! 
            string token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            await SendEmailAsync(
                           user.Email!,
                           token,
                           "active",
                           "confirm email",
                           "confirm email");
            //BackgroundJob.Enqueue<IMyAuth>(
            //         svc => svc.SendEmailAsync(
            //             user.Email!,
            //             token,
            //             "ConfirmEmail",
            //             "confirm email",
            //             "confirm email")
            //     );



            return "done";

        }
        [AutomaticRetry(Attempts = 5)]
        public async Task SendEmailAsync(string email, string code, string component, string subject, string message)
        {
            string from = _emailService.returnEmailFrom();
            var res = new EmailDto(email, from, subject,
                EmailStringBody.send(email, code, component, message));
            await _emailService.SendEmail(res);

        }

        public async Task<UserWithTokenDto> Login(LoginDto loginDto)
        {
            if (loginDto == null)
                throw new BadRequestException("Error,No Login Data Founded!");
            AppUser findUser = await CheckEmailForUserAsync(loginDto.Email);
            //email check ?
            if (!findUser.EmailConfirmed)
            {
                string tokenCodeForEmail = await _userManager.GenerateEmailConfirmationTokenAsync(findUser);
                await SendEmailAsync(
                             findUser.Email,
                             tokenCodeForEmail,
                             "active",
                             "confirm email",
                             "confirm email");

                throw new BadRequestException("Error,user email not Active! please check your email");

            }
            var result = await _signInManager.CheckPasswordSignInAsync(findUser, loginDto.password, true);
            if (!result.Succeeded)
            {
                throw new BadRequestException("Error,Email or Password is incorrect");

            }
            //create jwt
            string token = _generateToken.GetAndCreateToken(findUser);
            //return token with user obj
            var returnUser = new UserDto()
            {
                DisplayName = findUser.DisplayName,
                Email = findUser.Email,
                Id = findUser.Id,
                UserName = findUser.UserName
            };
            return new UserWithTokenDto()
            {
                token = token,
                user = returnUser
            };
        }

        public async Task<bool> SendEmailForForgetPassword(string email)
        {

            AppUser findUser = await CheckEmailForUserAsync(email);

            var tokeen = await _userManager.GeneratePasswordResetTokenAsync(findUser);
            string xx = "ResetPasswordEmail";
            await SendEmailAsync(findUser.Email, tokeen, "resetPassword", xx, xx);
            return true;


        }

        public async Task<string> ResetPassword(ResetPasswordDto resetPasswordDto)
        {
            AppUser findUser = await CheckEmailForUserAsync(resetPasswordDto.Email);
            IdentityResult? result =
                await _userManager.ResetPasswordAsync(findUser, resetPasswordDto.Token, resetPasswordDto.password);
            if (!result.Succeeded)
            {
                var tokeen = await _userManager.GeneratePasswordResetTokenAsync(findUser);
                string xx = "ResetPasswordEmail";
                await SendEmailAsync(findUser.Email, tokeen, "resetPassword", xx, xx);
                return GetIdentityErrors(result.Errors);
                //throw new BadRequestException(result.Errors.ToList().Select(x => x.Description).ToString());
            }
            return "done";
        }

        public async Task<bool> ActiveAccount(ActiveAccountDto activeAccountDto)
        {
            AppUser findUser = await CheckEmailForUserAsync(activeAccountDto.Email);
            var result = await _userManager.ConfirmEmailAsync(findUser, activeAccountDto.Token);
            if (!result.Succeeded)
            {
                string token = await _userManager.GenerateEmailConfirmationTokenAsync(findUser);
                await SendEmailAsync(findUser.Email, token, "active", "confirm email", "confirm email");
                return false;
            }
            return true;
        }

        public async Task<string> ChangePassword(ChangePasswordDto changePasswordDto)
        {
            var user = await _userManager.FindByEmailAsync(changePasswordDto.Email);
            if (changePasswordDto.NewPassword != changePasswordDto.ConfirmPassword)
                return "new password and confirm not match";
            var res = await _userManager
                .ChangePasswordAsync(user, changePasswordDto.oldPassword, changePasswordDto.NewPassword);
            if (!res.Succeeded)
            {
                return GetIdentityErrors(res.Errors);
            }
            return "done";
        }

        #region Helper
        private async Task<AppUser> CheckEmailForUserAsync(string email)
        {
            AppUser findUser = await _userManager.FindByEmailAsync(email);
            if (findUser == null)
                throw new BadRequestException("Error,Email or Password is incorrect");
            return findUser;
        }

        private string GetIdentityErrors(IEnumerable<IdentityError> errors)
        {
            return string.Join("; ", errors.Select(x => x.Description));
        }
        #endregion
    }
}
