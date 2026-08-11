using Ecom.Core.Entities.Discount;
using Ecom.Core.Entities.General;

namespace Ecom.Core.Interfaces
{
    public interface IDiscountProductRepo : IGenericRepo<TblProductDiscount, int>
    {
        Task<TblProductDiscount> AddDiscountProductAsync(TblDiscount Discount, int EntityId);
    }
}
