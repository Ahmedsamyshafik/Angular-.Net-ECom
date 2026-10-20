using System.Text.Json.Serialization;

namespace Ecom.Core.Entities.Order
{
    public class TblOrderItem : BaseEntity<int>
    {
        public TblOrderItem()
        {
        }

        public TblOrderItem(int productItemId, string mainImg, string productName, decimal price, int quentity)
        {
            ProductItemId = productItemId;
            MainImg = mainImg;
            ProductName = productName;
            Price = price;
            Quentity = quentity;
        }

        public int ProductItemId { get; set; }
        public string MainImg { get; set; }
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public int Quentity { get; set; }

        public int OrderId { get; set; }

        // Back-reference to the parent order; ignored in JSON to avoid a serialization cycle
        // (Order -> orderItems -> tblOrder -> orderItems -> ...).
        [JsonIgnore]
        public TblOrders tblOrder { get; set; }
    }
}
//