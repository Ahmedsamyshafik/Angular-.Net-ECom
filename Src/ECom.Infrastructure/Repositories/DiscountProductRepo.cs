using Ecom.Core.Entities.Discount;
using Ecom.Core.Entities.Tbl_Product;
using Ecom.Core.Interfaces;
using ECom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECom.Infrastructure.Repositories
{
    public class DiscountProductRepo : GenericRepo<TblProductDiscount, int>, IDiscountProductRepo
    {
        public DiscountProductRepo(AppDbContext context) : base(context)
        {
        }

        public async Task<TblProductDiscount> AddDiscountProductAsync(TblDiscount discount, int productId, CancellationToken cancellationToken = default)
        {
            var entity = new TblProductDiscount
            {
                CreatedAt = DateTime.UtcNow,
                Discount = discount,
                ProductId = productId
            };

            await _context.AddAsync(entity, cancellationToken);
            return entity;
        }

        public async Task<TblProductDiscount> AddDiscountProductAsync(TblDiscount discount, TblProducts product, CancellationToken cancellationToken = default)
        {
            var entity = new TblProductDiscount
            {
                CreatedAt = DateTime.UtcNow,
                Discount = discount,
                Product = product
            };

            await _context.AddAsync(entity, cancellationToken);
            return entity;
        }

        public async Task AddDiscountToProductsAsync(TblDiscount discount, IEnumerable<int> productIds, CancellationToken cancellationToken = default)
        {
            foreach (var productId in productIds)
            {
                var entity = new TblProductDiscount
                {
                    CreatedAt = DateTime.UtcNow,
                    Discount = discount,
                    ProductId = productId
                };
                await _context.AddAsync(entity, cancellationToken);
            }
        }

        public async Task<IReadOnlyList<TblProductDiscount>> GetByProductIdsAsync( IEnumerable<int> productIds,CancellationToken cancellationToken = default)
        {
            var ids = productIds?.Distinct().ToList() ?? new List<int>();

            if (ids.Count == 0)
            {
                return Array.Empty<TblProductDiscount>();
            }

            // Single query for the whole page of ids. The active date window is intentionally NOT applied
            // here so that IDiscountPriceCalculator remains the only definition of "active".
            return await _context.Set<TblProductDiscount>()
                .AsNoTracking()
                .Include(pd => pd.Discount)
                .Where(pd => ids.Contains(pd.ProductId) && pd.IsDeleted == false)
                .ToListAsync(cancellationToken);
        }
    }
}
