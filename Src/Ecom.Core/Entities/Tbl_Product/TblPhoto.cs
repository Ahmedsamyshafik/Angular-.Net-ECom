using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Ecom.Core.Entities.Tbl_Product
{
    public class TblPhoto : BaseEntity<int>
    {
        public string PhotoName { get; set; }
        public int ProductId { get; set; }
        [ForeignKey(nameof(ProductId))]
        public TblProducts? Product { get; set; } = new TblProducts();
    }
}
