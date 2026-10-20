using Ecom.Core.DTO.CartDto.Order;
using Ecom.Core.Entities.Order;

namespace Ecom.Core.Services
{
    public interface IOrderService
    {
        Task<TblOrders> CreateOrderAsync(string BuyerEmail, OrderDto orderDto, CancellationToken cancellationToken = default);
        Task ClearUserCartAsync(string BuyerEmail, CancellationToken cancellationToken = default);
        Task<List<OrderResponse>> GetAllOrdersForUserAsync(string BuyerEmail, CancellationToken cancellationToken = default);
        Task<TblOrders> GetOrderById(int OrderId, CancellationToken cancellationToken = default);
        Task<DeliveryMethodResponse> GetDeliveryMethodAsync(string BuyerEmail, int OrderId, CancellationToken cancellationToken = default);
        Task<List<DeliveryMethodResponse>> GetAllDeliveryMethodAsync(CancellationToken cancellationToken = default);
        Task<TblOrders> UpdateOrderToConfirm(int orderId);
    }
}
