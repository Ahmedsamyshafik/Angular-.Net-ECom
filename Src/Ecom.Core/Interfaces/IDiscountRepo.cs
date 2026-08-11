using Ecom.Core.Entities.General;

namespace Ecom.Core.Interfaces
{
    public interface IDiscountRepo : IGenericRepo<TblDiscount, int>
    {
        Task<TblDiscount> AddDiscountAsync(DateTime From, DateTime To, int TypeOfDiscount, int val, string? Name);
    }
}
