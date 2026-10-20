using System.Security.Claims;

using Asp.Versioning;

using Ecom.Core.DTO.CartDto.Order;
using Ecom.Core.Entities.Order;
using Ecom.Core.Exceptions;
using Ecom.Core.Interfaces;

using ECom.Api.Helper;
using ECom.Api.Registerations;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using Stripe;
using Stripe.Checkout;

namespace ECom.Api.Controllers.Payment.V1
{
    [Route("api/v{version:apiVersion}/Payment")]
    [ApiController]
    [ApiVersion("1.0")]
    [Authorize]
    [EnableRateLimiting(RateLimitingRegisteration.PerClientPolicyName)]
    public class PaymentController : MyBaseController
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IUnitOfWork unitOfWork,
            IConfiguration configuration,
            ILogger<PaymentController> logger)
            : base(unitOfWork)
        {
            _configuration = configuration;
            _logger = logger;
        }

        private string email => User.Claims.FirstOrDefault(m => m.Type == ClaimTypes.Email)?.Value;

        [HttpPost("GeneratePayment")]
        public async Task<IActionResult> GeneratePayment()
        {

            var order = await _unitOfWork.OrderService.GetAllOrdersForUserAsync(email);
            OrderResponse? currentOrder = order.Where(x => x.Status == Status.Pending.ToString()).OrderByDescending(x => x.CreatedAt).FirstOrDefault();
            //validate current order is exist !
            var result = await HandlePayment(currentOrder);
            return Ok(new responseApi(200, "", result));
        }


        [HttpPost("Webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook()
        {


            var json = await new StreamReader(Request.Body).ReadToEndAsync();

            var signature = Request.Headers["Stripe-Signature"].ToString();
            var webhookSecret = _configuration["Stripe:WebhookSecret"];

            if (string.IsNullOrWhiteSpace(webhookSecret))
            {
                _logger.LogError("Stripe webhook secret is missing.");
                return StatusCode(500);
            }

            Event stripeEvent;

            try
            {
                stripeEvent = EventUtility.ConstructEvent(
                    json,
                    signature,
                    webhookSecret,
                    throwOnApiVersionMismatch: false
                );
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Invalid Stripe webhook signature.");
                return BadRequest();
            }

            if (stripeEvent.Type == "checkout.session.completed")
            {
                if (stripeEvent.Data.Object is not Session session)
                    return BadRequest();

                if (session.PaymentStatus != "paid")
                {
                    _logger.LogInformation(
                        "Checkout completed but payment is not paid. Session: {SessionId}",
                        session.Id);

                    return Ok();
                }

                if (!int.TryParse(session.ClientReferenceId, out var orderId))
                {
                    _logger.LogWarning(
                        "Missing or invalid OrderId in Stripe session {SessionId}",
                        session.Id);

                    return BadRequest();
                }

                _logger.LogInformation(
                    "Payment confirmed. OrderId: {OrderId}, SessionId: {SessionId}",
                    orderId,
                    session.Id);

                // Next step:
                // 1. Update this order to Paid.

                // 2. Confirm its stock reservation.

                // 3. Clear the appropriate cart.
                //TryConfirmReservationAsync - DeleteCartAsync - updateOrderToConfirm
                var x = await _unitOfWork.OrderService.UpdateOrderToConfirm(orderId);
                var user = await _unitOfWork.myAuth.GetUserByEmail(x.BuyerEmail);

                await _unitOfWork.StockReservationService.GetReservationThenConfirm(orderId, user.Id);

                await _unitOfWork.Commit();


                //await _unitOfWork.CartProductFacade.DeleteCartAsync(user.Id); // Already Removed

            }

            return Ok();


        }

        private async Task<string> HandlePayment(OrderResponse order)
        {
            try
            {
                var options = new SessionCreateOptions
                {
                    ClientReferenceId = order.OrderId.ToString(),

                    Mode = "payment",
                    SuccessUrl =
                             "http://localhost:4200/Success?session_id={CHECKOUT_SESSION_ID}",
                    CancelUrl = "http://localhost:4200/Shop",
                    AllowedPaymentMethodTypes = new List<string> { "card" },
                    LineItems = order.items.Select(item => new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = item.ProductName
                            },
                            UnitAmount = (long)(item.Price * 100) // Convert to cents
                        },
                        Quantity = item.Quentity
                    }).ToList(),

                };

                var service = new SessionService();
                var session = await service.CreateAsync(options);

                //return  sessionId = session.Id;
                return session.Url!;
            }
            catch (Exception ex)
            {
                // Log the error
                //return StatusCode(500, $"Internal server error: {ex.Message}");
                throw new BadRequestException("ERRROOOORRR");
            }
        }
    }
}
