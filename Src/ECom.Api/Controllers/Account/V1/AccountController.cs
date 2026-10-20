using System.Security.Claims;

using Asp.Versioning;

using Ecom.Core.DTO.CartDto.Order;
using Ecom.Core.DTO.UserDto;
using Ecom.Core.Entities.Order;
using Ecom.Core.Exceptions;
using Ecom.Core.Interfaces;

using ECom.Api.DTO;
using ECom.Api.Helper;
using ECom.Infrastructure.Repositories;

using Microsoft.AspNetCore.Authorization;
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
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto, CancellationToken cancellationToken = default)
        {
            var res = await _unitOfWork.myAuth.RegisterAsync(registerDto, cancellationToken);
            if (res != "done")
            {
                throw new BadRequestException(res);
            }
            return Ok(new responseApi(200, "register Done ! now go to login after active email ", null));
        }
        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto, CancellationToken cancellationToken = default)
        {
            var res = await _unitOfWork.myAuth.Login(loginDto, cancellationToken);
            if (res == null)
                throw new BadRequestException("some thing is wrong!");
            // Transfer/merge the guest basket into the authenticated user's basket. The guest
            // id comes from the request body (loginDto.cartGuestId) or, when the SPA did not
            // send it, from the server-issued guest cookie. The guest cookie is cleared once
            // the merge succeeds.
            var guestCartId = loginDto.cartGuestId;
            if (string.IsNullOrWhiteSpace(guestCartId))
            {
                Request.Cookies.TryGetValue(GuestCartCookie.Name, out guestCartId);
            }

            if (!string.IsNullOrWhiteSpace(guestCartId))
            {
                await _unitOfWork.CartProductFacade.UpdateCartIdForGuestUsers(guestCartId, res.user.Id, cancellationToken);
                GuestCartCookie.Clear(HttpContext);
            }
            Response.Cookies.Append("token", res.token, new CookieOptions
            {
                //Secure=true, https

                HttpOnly = true,
                Domain = "localhost",
                Expires = DateTime.Now.AddDays(1),
                IsEssential = true,
                SameSite = SameSiteMode.Strict

            });
            return Ok(new responseApi(200, "success", res.user));
        }

        [HttpPost("ForgetPassword")]
        public async Task<IActionResult> ForgetPassword([FromBody] ForgetPasswordDto forgetPasswordDto, CancellationToken cancellationToken = default)
        {

            bool res = await _unitOfWork.myAuth.SendEmailForForgetPassword(forgetPasswordDto.email, cancellationToken);
            if (!res)
                throw new BadRequestException("some thing is wrong!");
            return Ok(new responseApi(200, "success", null));

        }
        [HttpPost("ResetPassword")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto, CancellationToken cancellationToken = default)
        {
            string res = await _unitOfWork.myAuth.ResetPassword(resetPasswordDto, cancellationToken);
            if (res != "done")
                throw new BadRequestException(res);

            return Ok(new responseApi(200, "success", null));

        }

        [HttpPost("ActiveAccount")]
        public async Task<IActionResult> ActiveAccount([FromBody] ActiveAccountDto activeAccountDto, CancellationToken cancellationToken = default)
        {
            var res = await _unitOfWork.myAuth.ActiveAccount(activeAccountDto, cancellationToken);
            if (!res)
                throw new BadRequestException("Token is incorrect , New token Sended ! ");

            return Ok(new responseApi(200, "success", null));

        }
        /// <summary>
        /// Changes the password of the signed-in user. The target account is resolved from the
        /// authenticated claims (NameIdentifier), never from an email in the body; the current
        /// password is still verified by Identity.
        /// </summary>
        [HttpPost("ChangePassword")]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto changePasswordDto, CancellationToken cancellationToken = default)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                throw new BadRequestException("No user id founded for this session !");

            string res = await _unitOfWork.myAuth.ChangePassword(userId, changePasswordDto, cancellationToken);
            if (res != "done")
            {
                throw new BadRequestException(res);
            }
            return Ok(new responseApi(200, "success", null));
        }

        /// <summary>
        /// Creates or replaces the signed-in user's address. The row lives in the ONE
        /// shared address table (tblShippingAddresses) that also stores the order
        /// shipping addresses, so the database keeps a single address table handling
        /// both the user side (AppUserId) and the order side (OrderId).
        /// </summary>
        [HttpPut("UpdateAddress")]
        [Authorize]
        public async Task<IActionResult> UpdateAddress([FromBody] shipAddressDto dto, CancellationToken cancellationToken = default)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                throw new BadRequestException("No user id founded for this session !");

            var address = await _unitOfWork.ShippingAddressesRepo.GetByUserAsync(userId, cancellationToken);

            if (address is null)
            {
                address = new TblShippingAddress
                {
                    AppUserId = userId,
                    CreatedBy = userId
                };
                ApplyAddress(address, dto);
                await _unitOfWork.ShippingAddressesRepo.AddAsync(address, cancellationToken);
            }
            else
            {
                ApplyAddress(address, dto);
                address.UpdatedAt = DateTime.UtcNow;
                address.UpdatedBy = userId;
                await _unitOfWork.ShippingAddressesRepo.UpdateAsync(address, cancellationToken);
            }

            await _unitOfWork.Commit(cancellationToken);

            return Ok(new responseApi(200, "Address updated successfully", address));
        }

        /// <summary>
        /// Returns the signed-in user's saved address (the tblShippingAddresses row whose
        /// AppUserId is the current user) as a DTO, or 404 when the user never saved one.
        /// Counterpart of UpdateAddress; the row layout is documented there.
        /// </summary>
        [HttpGet("GetAddress")]
        [Authorize]
        public async Task<IActionResult> GetAddress(CancellationToken cancellationToken = default)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                throw new BadRequestException("No user id founded for this session !");

            var address = await _unitOfWork.ShippingAddressesRepo.GetByUserAsync(userId, cancellationToken);
            if (address is null)
                throw new NotFoundException("No address saved for this user yet !");

            return Ok(new responseApi(200, "success", ToDto(address)));
        }

        private static void ApplyAddress(TblShippingAddress address, shipAddressDto dto)
        {
            address.FirstName = dto.FirstName;
            address.LastName = dto.LastName;
            address.City = dto.City;
            address.ZipCode = dto.ZipCode;
            address.Street = dto.Street;
            address.State = dto.State;
        }

        private static shipAddressDto ToDto(TblShippingAddress address) => new()
        {
            FirstName = address.FirstName,
            LastName = address.LastName,
            City = address.City,
            ZipCode = address.ZipCode,
            Street = address.Street,
            State = address.State
        };
    }
}
