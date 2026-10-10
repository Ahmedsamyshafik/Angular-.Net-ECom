namespace Ecom.Core.Entities.Tbl_Product
{
    public class TblPhoto : BaseEntity<int>
    {
        public string PhotoPath { get; set; }

        // Nullable Foreign Keys للجداول
        //products
        public int? ProductId { get; set; }
        public TblProducts? Product { get; set; }

        //categories
        public int? CategoryId { get; set; }
        public TblCategories? Category { get; set; }
    }
}
