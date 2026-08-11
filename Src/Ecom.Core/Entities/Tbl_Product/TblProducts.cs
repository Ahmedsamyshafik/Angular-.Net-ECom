using System.ComponentModel.DataAnnotations.Schema;

namespace Ecom.Core.Entities.Tbl_Product
{
    public class TblProducts : BaseEntity<int>
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int CategoryId { get; set; }

        [ForeignKey(nameof(CategoryId))]
        public TblCategories? Category { get; set; }
        public ICollection<TblPhoto>? Photos { get; set; } = new HashSet<TblPhoto>();
    }
}
