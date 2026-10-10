using Ecom.Core.DTO.Products;
using Ecom.Core.Entities.Tbl_Product;
using Ecom.Core.Interfaces;
using Ecom.Core.Pagination;
using ECom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECom.Infrastructure.Repositories
{
    public class ProductRepo : GenericRepo<TblProducts, int>, IProductRepo
    {
        public ProductRepo(AppDbContext context) : base(context)
        {
        }

        public async Task<Pagination<TblProducts>> GetPageAsync(ProductQueryParams query, CancellationToken cancellationToken = default)
        {
            IQueryable<TblProducts> queryable = _context.Set<TblProducts>()
                .AsNoTracking()
                .Where(p => p.IsDeleted == false)
                .Include(p => p.Category)
                .Include(p => p.Photos);

            if (query.CategoryId.HasValue)
            {
                queryable = queryable.Where(p => p.CategoryId == query.CategoryId.Value);
            }

            if (query.MinPrice.HasValue)
            {
                queryable = queryable.Where(p => p.Price >= query.MinPrice.Value);
            }

            if (query.MaxPrice.HasValue)
            {
                queryable = queryable.Where(p => p.Price <= query.MaxPrice.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var terms = query.Search
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var term in terms)
                {
                    queryable = queryable
                        .Where(p => p.Name.Contains(term) || p.Description.Contains(term));
                }
            }

            int totalCount = await queryable.CountAsync(cancellationToken);

            string sortBy = string.IsNullOrWhiteSpace(query.SortBy) ? "id" : query.SortBy.Trim().ToLowerInvariant();
            bool descending = string.Equals(query.SortOrder?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);

            IOrderedQueryable<TblProducts> ordered = sortBy switch
            {
                "name" => descending
                    ? queryable.OrderByDescending(p => p.Name).ThenByDescending(p => p.Id)
                    : queryable.OrderBy(p => p.Name).ThenBy(p => p.Id),

                "price" => descending
                    ? queryable.OrderByDescending(p => p.Price).ThenByDescending(p => p.Id)
                    : queryable.OrderBy(p => p.Price).ThenBy(p => p.Id),

                "createdat" => descending
                    ? queryable.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
                    : queryable.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id),

                _ => descending
                    ? queryable.OrderByDescending(p => p.Id)
                    : queryable.OrderBy(p => p.Id),
            };

            var data = await ordered
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);

            return new Pagination<TblProducts>(query.PageNumber, query.PageSize, totalCount, data);
        }
    }
}