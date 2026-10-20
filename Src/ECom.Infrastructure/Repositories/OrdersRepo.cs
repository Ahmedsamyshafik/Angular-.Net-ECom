using Ecom.Core.DTO.CartDto.Order;
using Ecom.Core.Entities.Order;
using Ecom.Core.Interfaces;

using ECom.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;

namespace ECom.Infrastructure.Repositories
{
    public class OrdersRepo : GenericRepo<TblOrders, int>, IOrdersRepo
    {
        public OrdersRepo(AppDbContext context) : base(context)
        {
        }

        public async Task<List<TblOrders>> GetUserOrdersByEmail(string BuyerEmail, CancellationToken cancellationToken = default)
        {
            return await _context.tblOrders.Where(x => x.BuyerEmail == BuyerEmail && x.status != Status.PaymentFaild).Include(x => x.deliveryMethod).Include(x => x.orderItems).Include(x => x.shippingAddress).ToListAsync(cancellationToken);
        }
        public async Task<TblOrders> GeOrdersWithMethodDeliveryById(int OrderId, CancellationToken cancellationToken = default)
        {
            return await _context.tblOrders.Where(x => x.Id == OrderId).Include(x => x.orderItems).Include(x => x.deliveryMethod).Include(x => x.shippingAddress).FirstOrDefaultAsync(cancellationToken);
        }
        public async Task<DeliveryMethodResponse> GeMethodDeliveryWithByOrderId(int OrderId, CancellationToken cancellationToken = default)
        {

            return await _context.tblOrders
                 .Where(o => o.Id == OrderId)
                 .Select(o => new DeliveryMethodResponse
                 {
                     id = o.deliveryMethod.Id,
                     name = o.deliveryMethod.Name,
                     price = o.deliveryMethod.Price
                 }).FirstOrDefaultAsync(cancellationToken);


        }
    }
}
