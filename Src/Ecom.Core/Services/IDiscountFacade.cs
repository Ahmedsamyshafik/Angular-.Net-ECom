using Ecom.Core.DTO.Products;
using System.Threading;
using System.Threading.Tasks;

namespace Ecom.Core.Services
{
    public interface IDiscountFacade
    {
        Task ApplyDiscountAsync(AddDiscountDto discount, CancellationToken cancellationToken = default);

        Task ApplyCategoryDiscountAsync(AddDiscountDto discount, CancellationToken cancellationToken = default);
    }
}