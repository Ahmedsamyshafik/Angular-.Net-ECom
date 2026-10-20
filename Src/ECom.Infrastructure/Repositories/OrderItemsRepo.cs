using Ecom.Core.Entities.Order;
using Ecom.Core.Interfaces;

using ECom.Infrastructure.Data;

namespace ECom.Infrastructure.Repositories
{
    public class OrderItemsRepo : GenericRepo<TblOrderItem, int>, IOrderItemsRepo
    {
        public OrderItemsRepo(AppDbContext context) : base(context)
        {
        }
    }
}
