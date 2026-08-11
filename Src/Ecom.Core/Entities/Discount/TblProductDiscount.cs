using Ecom.Core.Entities.General;
using Ecom.Core.Entities.Tbl_Product;

namespace Ecom.Core.Entities.Discount
{
    public class TblProductDiscount : BaseEntity<int>
    {
        public int productId { get; set; }
        public TblProducts Product { get; set; } = null!;
        public int DiscountId { get; set; }
        public TblDiscount Discount { get; set; } = null!;

    }
}
