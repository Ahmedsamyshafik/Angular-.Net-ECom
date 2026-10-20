using Ecom.Core.DTO.Categories;
using Ecom.Core.Entities.Tbl_Product;
using Ecom.Core.Interfaces;
using Ecom.Core.Pagination;

using ECom.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;

namespace ECom.Infrastructure.Repositories
{
    public class CategoryRepo : GenericRepo<TblCategories, int>, ICategoryRepo
    {
        public CategoryRepo(AppDbContext context) : base(context)
        {
        }

        public async Task<Pagination<TblCategories>> GetPageAsync(CategoryQueryParams query, CancellationToken cancellationToken = default)
        {
            IQueryable<TblCategories> queryable = _context.Set<TblCategories>()
                .AsNoTracking()
                .Where(c => c.IsDeleted == false)
                .Include(c => c.Photos);

            //Get Categories which only have products?
            if (query.HasProducts.HasValue)
            {
                queryable = query.HasProducts.Value
                    ? queryable.Where(c => c.Products!.Any(p => p.IsDeleted == false))
                    : queryable.Where(c => !c.Products!.Any(p => p.IsDeleted == false));
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                //search using every word in the search!
                var terms = query.Search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var term in terms)
                {
                    queryable = queryable.Where(c => c.Name.Contains(term) || c.Description.Contains(term));
                }
            }

            int totalCount = await queryable.CountAsync(cancellationToken);

            string sortBy = string.IsNullOrWhiteSpace(query.SortBy) ? "id" : query.SortBy.Trim().ToLowerInvariant();
            bool descending = string.Equals(query.SortOrder?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);

            IOrderedQueryable<TblCategories> ordered = sortBy switch
            {
                "name" => descending
                    ? queryable.OrderByDescending(c => c.Name).ThenByDescending(c => c.Id)
                    : queryable.OrderBy(c => c.Name).ThenBy(c => c.Id),

                "createdat" => descending
                    ? queryable.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
                    : queryable.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id),

                _ => descending
                    ? queryable.OrderByDescending(c => c.Id)
                    : queryable.OrderBy(c => c.Id),
            };

            var data = await ordered
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);



            return new Pagination<TblCategories>(query.PageNumber, query.PageSize, totalCount, data);
        }
    }
}