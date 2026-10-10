using Ecom.Core.Entities.Discount;
using Ecom.Core.Interfaces;
using ECom.Infrastructure.Data;

namespace ECom.Infrastructure.Repositories
{
    public class DiscountRepo : GenericRepo<TblDiscount, int>, IDiscountRepo
    {
        public DiscountRepo(AppDbContext context) : base(context)
        {
        }
    }
}