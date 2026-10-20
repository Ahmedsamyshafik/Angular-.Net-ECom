using Ecom.Core.Entities.Order;
using Ecom.Core.Interfaces;

using ECom.Infrastructure.Data;

namespace ECom.Infrastructure.Repositories
{
    public class DeliveryMethodsRepo : GenericRepo<TblDeliveryMethod, int>, IDeliveryMethodsRepo
    {
        public DeliveryMethodsRepo(AppDbContext context) : base(context)
        {
        }
    }
}
