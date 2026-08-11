namespace Ecom.Core.Entities.Tbl_Product
{
    public class TblCategories : BaseEntity<int>
    {
        public string Name { get; set; }
        public string Description { get; set; }

        public ICollection<int>? ProductIds { get; set; }
        public ICollection<TblProducts>? Products { get; set; } = new HashSet<TblProducts>();
        public ICollection<TblPhoto>? Photos { get; set; } = new HashSet<TblPhoto>();
    }
}
