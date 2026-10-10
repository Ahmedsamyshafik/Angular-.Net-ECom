using Ecom.Core.DTO.Categories;
using Ecom.Core.Entities.Tbl_Product;
using Ecom.Core.Pagination;
using System.Threading;
using System.Threading.Tasks;

namespace Ecom.Core.Interfaces
{
    public interface ICategoryRepo : IGenericRepo<TblCategories, int>
    {
        Task<Pagination<TblCategories>> GetPageAsync(CategoryQueryParams query, CancellationToken cancellationToken = default);
    }
}