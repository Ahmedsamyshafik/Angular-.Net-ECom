namespace Ecom.Core.Entities.Tbl_Product
{
    public class TblPhoto : BaseEntity<int>
    {
        //Entities Types and names
        public string EntityType { get; set; }//product , category, user, etc
        public string EntityId { get; set; }

        public string PhotoPath { get; set; }




        // Nullable Foreign Keys للجداول
        //products
        public int? ProductId { get; set; }
        public TblProducts? Product { get; set; }

        //categories
        public int? CategoryId { get; set; }
        public TblCategories? Category { get; set; }

        //public string? UserId { get; set; } // Guid as string
        //public ApplicationUser? User { get; set; }



    }
}
