namespace Ecom.Core.Entities.Order
{
    public class TblDeliveryMethod : BaseEntity<int>
    {
        public TblDeliveryMethod()
        {
        }

        public TblDeliveryMethod(string name, string description, decimal price, string deliveryTime)
        {
            Name = name;
            Description = description;
            Price = price;
            DeliveryTime = deliveryTime;
        }

        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string DeliveryTime { get; set; }
    }
}