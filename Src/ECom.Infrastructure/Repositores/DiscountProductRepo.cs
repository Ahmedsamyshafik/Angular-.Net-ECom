using Ecom.Core.Entities.Discount;
using Ecom.Core.Entities.General;
using Ecom.Core.Interfaces;
using ECom.Infrastructure.Data;

namespace ECom.Infrastructure.Repositores
{
    public class DiscountProductRepo : GenericRepo<TblProductDiscount, int>, IDiscountProductRepo
    {
        public DiscountProductRepo(AppDbContext context) : base(context)
        {
        }

        public async Task<TblProductDiscount> AddDiscountProductAsync(TblDiscount Discount, int EntityId)
        {


            var disProduct = new TblProductDiscount()
            {
                CreatedAt = DateTime.UtcNow,
                CreatedBy = 11,
                Discount = Discount,
                productId = EntityId
            };

            await _context.AddAsync(disProduct);
            return disProduct;
        }

    }
}
