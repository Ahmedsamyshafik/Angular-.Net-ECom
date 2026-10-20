using Ecom.Core.DTO.CartDto.Order;
using Ecom.Core.Entities.Order;

namespace Ecom.Core.Interfaces
{
    public interface IOrdersRepo : IGenericRepo<TblOrders, int>
    {
        Task<List<TblOrders>> GetUserOrdersByEmail(string BuyerEmail, CancellationToken cancellationToken = default);
        Task<TblOrders> GeOrdersWithMethodDeliveryById(int OrderId, CancellationToken cancellationToken = default);
        Task<DeliveryMethodResponse> GeMethodDeliveryWithByOrderId(int OrderId, CancellationToken cancellationToken = default);
    }
}
