using Ecom.Core.DTO.Products;
using Ecom.Core.Entities.Tbl_Product;
using Ecom.Core.Pagination;
using System.Threading;
using System.Threading.Tasks;

namespace Ecom.Core.Interfaces
{
    public interface IProductRepo : IGenericRepo<TblProducts , int>
    {
        Task<Pagination<TblProducts>> GetPageAsync(ProductQueryParams query, CancellationToken cancellationToken = default);
    }
}