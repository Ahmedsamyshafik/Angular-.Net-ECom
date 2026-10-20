using System.Security.Claims;

using Asp.Versioning;

using AutoMapper;

using Ecom.Core.DTO.CartDto.Order;
using Ecom.Core.Interfaces;

using ECom.Api.Helper;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECom.Api.Controllers.Order.V1
{
    [Route("api/v{version:apiVersion}/Order")]
    [ApiVersion("1.0")]

    [ApiController]
    [Authorize]
    public class OrderController : MyBaseController
    {
        private readonly IMapper _mapper;

        public OrderController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork)
        {
            _mapper = mapper;
        }

        private string email => User.Claims.FirstOrDefault(m => m.Type == ClaimTypes.Email)?.Value;

        [HttpPost("CreateOrder")]
        public async Task<IActionResult> Create([FromBody] OrderDto orderDto, CancellationToken cancellationToken = default)
        {

            var res = await _unitOfWork.OrderService.CreateOrderAsync(email, orderDto, cancellationToken);

            // Repositories only track changes; commit here (same pattern as Product/Category).
            // At this point the order is persisted with status Pending and its stock
            // reservations are AttachedToOrder.
            await _unitOfWork.Commit(cancellationToken);

            // =====================================================================
            // TODO (future payment confirmation point)
            // ---------------------------------------------------------------------
            // Once a payment provider is integrated, payment confirmation MUST:
            //  1. Conditionally transition each AttachedToOrder reservation belonging to
            //     this order to Confirmed - use IStockReservationService
            //     .TryConfirmReservationAsync(reservationId), which is atomic and guarded by
            //     the order still being Pending. Never mark 'Partial' states; the confirm
            //     update affects 0 rows when the reservation was already expired/released.
            //  2. Use conditional, RowVersion-protected updates and the same transaction as
            //     the order status change (Pending -> PaymentReceived). A DbUpdateConcurrency
            //     Exception must surface as a 409 (already mapped in GlobalExceptionHandler),
            //     so a stale webhook retry can never double-confirm.
            //  3. Idempotency: webhook handlers must be retried safely. Confirmations from
            //     Duplicate webhoook events must each hit TryConfirmReservationAsync; only the
            //     first call wins, subsequent calls return false and restore/decrement nothing.
            //  4. Never restore (increment) stock on confirmation - a confirmed purchase owns
            //     the reserved quantity permanently. Restoring happens exactly once per
            //     reservation, ONLY in IStockReservationService.ExpireReservationAsync.
            //  5. The reservation FAQ: expiry-vs-purchase race. If ExpireReservationAsync wins
            //     (order still Pending when the 3-day window ends) it flips the order to
            //     PaymentFaild and releases the stock; TryConfirmReservationAsync afterwards
            //     returns false, so the purchase is refused rather than silently fulfilled.
            //     If confirmation wins first, ExpireReservationAsync no-ops (status gate).
            // =====================================================================

            // Clear the basket only after the order is safely persisted.
            await _unitOfWork.OrderService.ClearUserCartAsync(email, cancellationToken);

            // Respond with the read model, never the TblOrders entity (which would leak
            // the whole persistence graph, e.g. RowVersion and navigation properties).
            CreateOrderResponse? response = _mapper.Map<CreateOrderResponse>(res);

            return Ok(new responseApi(200, "success", response));
        }
        [HttpGet("GetAllOrders")]
        public async Task<IActionResult> GetAllOrdersForUser(CancellationToken cancellationToken = default)
        {
            var res = await _unitOfWork.OrderService.GetAllOrdersForUserAsync(email, cancellationToken);
            return Ok(new responseApi(200, "success", res));

        }
        [HttpGet("GetDeliveryMethod")]
        public async Task<IActionResult> GetDeliveryMethod([FromQuery] int OrderId, CancellationToken cancellationToken = default)
        {
            var res = await _unitOfWork.OrderService.GetDeliveryMethodAsync(email, OrderId, cancellationToken);
            return Ok(new responseApi(200, "success", res));

        }
        [HttpGet("GetAllDeliveryMethod")]
        public async Task<IActionResult> GetAllDeliveryMethod(CancellationToken cancellationToken = default)
        {
            var res = await _unitOfWork.OrderService.GetAllDeliveryMethodAsync(cancellationToken);
            return Ok(new responseApi(200, "success", res));

        }
    }
}
