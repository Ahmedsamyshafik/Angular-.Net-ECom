using Asp.Versioning;

using Ecom.Core.DTO.UserDto;
using Ecom.Core.Exceptions;
using Ecom.Core.Interfaces;

using ECom.Api.DTO;
using ECom.Api.Helper;
using ECom.Infrastructure.Repositories;

using Microsoft.AspNetCore.Mvc;

namespace ECom.Api.Controllers.Account.V1
{
    [Route("api/v{version:apiVersion}/Account")]
    [ApiVersion("1.0")]

    [ApiController]
    public class AccountController : MyBaseController
    {
        public AccountController(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }
        [HttpPost("Register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            var res = await _unitOfWork.myAuth.RegisterAsync(registerDto);
            if (res != "done")
            {
                throw new BadRequestException(res);
            }
            return Ok(new responseApi(200, "register Done ! now go to login after active email ", null));
        }
        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            var res = await _unitOfWork.myAuth.Login(loginDto);
            if (res == null)
                throw new BadRequestException("some thing is wrong!");
            Response.Cookies.Append("token", res.token,new CookieOptions
            {
                //Secure=true, https
                
                HttpOnly=true,
                Domain ="localhost",
                Expires=DateTime.Now.AddDays(1),
                IsEssential=true,
                SameSite=SameSiteMode.Strict

            });
            return Ok(new responseApi(200, "success", res.user));
        }

        [HttpPost("ForgetPassword")]
        public async Task<IActionResult> ForgetPassword([FromBody] ForgetPasswordDto forgetPasswordDto)
        {

            bool res = await _unitOfWork.myAuth.SendEmailForForgetPassword(forgetPasswordDto.email);
            if (!res)
                throw new BadRequestException("some thing is wrong!");
            return Ok(new responseApi(200, "success", null));

        }
        [HttpPost("ResetPassword")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
        {
            string res = await _unitOfWork.myAuth.ResetPassword(resetPasswordDto);
            if (res != "done")
                throw new BadRequestException(res);

            return Ok(new responseApi(200, "success", null));

        }

        [HttpPost("ActiveAccount")]
        public async Task<IActionResult> ActiceAccount([FromBody] ActiveAccountDto activeAccountDto)
        {
            var res = await _unitOfWork.myAuth.ActiveAccount(activeAccountDto);
            if (!res)
                throw new BadRequestException("Token is incorrect , New token Sended ! ");

            return Ok(new responseApi(200, "success", null));

        }
        [HttpPost("ChangePassword")]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto changePasswordDto)
        {
            string res = await _unitOfWork.myAuth.ChangePassword(changePasswordDto);
            if (res != "done")
            {
                throw new BadRequestException(res);
            }
            return Ok(new responseApi(200, "success", null));
        }
    }
}
